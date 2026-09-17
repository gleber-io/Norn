using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Norn.Contracts;
using Norn.Planner.Settings;
using Norn.Planner.Telemetry;
using Norn.Planner.Validation;
using OllamaSharp;
using OllamaSharp.Models;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner.LlmPlanning;

/// <summary>
/// Braço B (§5.5) — pipeline completo: orçamento → chamada → validação → reparo → fallback.
/// Nenhuma exceção do conector escapa para o chamador: todo modo de falha vira um motivo em
/// <c>llmTrace.failureReasons</c> e o pipeline cai para o <c>RuleEngine</c> (braço C).
/// </summary>
public sealed partial class LlmPlanner(
    IChatClient chatClient,
    PromptBuilder promptBuilder,
    LlmOutputValidator validator,
    RuleEngineImpl ruleEngine,
    PlannerOptions options,
    PlannerMetrics metrics,
    TimeProvider timeProvider,
    ILogger<LlmPlanner> logger)
{
    public async Task<HealingPlan> DecideAsync(AnomalyContext context, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        var builtPrompt = promptBuilder.Build(context);
        var failureReasons = new List<string>();

        if (!builtPrompt.WithinBudget)
        {
            LogPromptBudgetExceeded(logger, builtPrompt.TotalChars);
            failureReasons.Add(LlmFailureReasons.PromptBudgetExceeded);
            return FallBackToRuleEngine(context, builtPrompt.PromptHash, attempts: 0, failureReasons, startedAt);
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, builtPrompt.SystemPrompt),
            new(ChatRole.User, builtPrompt.ContextJson),
        };

        var attempt = await CallAndValidateAsync(messages, context, cancellationToken).ConfigureAwait(false);
        var attempts = 1;

        if (!attempt.Result.Success)
        {
            failureReasons.Add(attempt.Result.FailureReason!);
            metrics.RecordRepairAttempt();

            messages.Add(new ChatMessage(ChatRole.Assistant, attempt.RawResponse ?? string.Empty));
            messages.Add(new ChatMessage(ChatRole.User,
                $"A resposta anterior falhou na validação: {attempt.Result.FailureReason} — {attempt.Result.ErrorDetail}. " +
                "Responda de novo, somente com o JSON corrigido, no mesmo formato."));

            attempt = await CallAndValidateAsync(messages, context, cancellationToken).ConfigureAwait(false);
            attempts = 2;
        }

        if (!attempt.Result.Success)
        {
            failureReasons.Add(attempt.Result.FailureReason!);
            return FallBackToRuleEngine(context, builtPrompt.PromptHash, attempts, failureReasons, startedAt);
        }

        var plan = attempt.Result.Plan!;
        var healingPlan = new HealingPlan
        {
            PlanId = Guid.NewGuid(),
            ContextId = context.ContextId,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            DecidedBy = DecidedBy.Llm,
            Rationale = Truncate(plan.Rationale, 500),
            Confidence = plan.Confidence,
            Actions = plan.Actions,
            ExpectedOutcome = plan.ExpectedOutcome,
            VerificationWindowSeconds = options.VerificationWindowSecondsFor(
                plan.Actions.Count > 0 ? plan.Actions[0].Type : HealingActionType.NoOp),
            LlmTrace = new LlmTrace
            {
                PromptHash = builtPrompt.PromptHash,
                Model = options.ModelName,
                LatencyMs = ElapsedMs(startedAt),
                Attempts = attempts,
                FailureReasons = failureReasons,
            },
        };

        metrics.RecordPlanningLatency(timeProvider.GetElapsedTime(startedAt), DecidedBy.Llm);
        return healingPlan;
    }

    private async Task<(LlmValidationResult Result, string? RawResponse)> CallAndValidateAsync(
        IReadOnlyList<ChatMessage> messages, AnomalyContext context, CancellationToken cancellationToken)
    {
        var chatOptions = new ChatOptions { Temperature = 0f }
            .AddOllamaOption(OllamaOption.Seed, options.Seed)
            .AddOllamaOption(OllamaOption.NumCtx, options.NumCtx);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.OllamaTimeout);

        string rawResponse;
        try
        {
            var response = await chatClient.GetResponseAsync(messages, chatOptions, timeoutCts.Token).ConfigureAwait(false);
            rawResponse = response.Text;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogOllamaTimeout(logger, options.OllamaTimeout);
            return (LlmValidationResult.Fail(LlmFailureReasons.Timeout, $"Sem resposta em {options.OllamaTimeout.TotalSeconds:0}s."), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falha de conector (conexão recusada, servidor fora do ar) não é um dos cinco motivos
            // fixos do §5.5, mas precisa do mesmo tratamento: nunca propagar, sempre cair no
            // fallback com o motivo registrado.
            LogOllamaConnectorFailed(logger, ex);
            return (LlmValidationResult.Fail(LlmFailureReasons.ConnectorError, ex.Message), null);
        }

        return (validator.Validate(rawResponse, context), rawResponse);
    }

    private HealingPlan FallBackToRuleEngine(
        AnomalyContext context, string promptHash, int attempts, List<string> failureReasons, long startedAt)
    {
        var rulePlan = ruleEngine.Decide(context);
        var trace = new LlmTrace
        {
            PromptHash = promptHash,
            Model = options.ModelName,
            LatencyMs = ElapsedMs(startedAt),
            Attempts = attempts,
            FailureReasons = failureReasons,
        };

        metrics.RecordFallback(failureReasons.Count > 0 ? failureReasons[^1] : "Unknown");
        metrics.RecordPlanningLatency(timeProvider.GetElapsedTime(startedAt), rulePlan.DecidedBy);

        return rulePlan with { LlmTrace = trace };
    }

    private long ElapsedMs(long startedAt) => (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt acima do orçamento ({TotalChars} caracteres) — caindo no fallback sem chamar o Ollama.")]
    private static partial void LogPromptBudgetExceeded(ILogger logger, int totalChars);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeout de {Timeout} na chamada ao Ollama.")]
    private static partial void LogOllamaTimeout(ILogger logger, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Error, Message = "Chamada ao conector Ollama falhou — caindo no fallback.")]
    private static partial void LogOllamaConnectorFailed(ILogger logger, Exception exception);
}
