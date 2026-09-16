using System.Text.Json;
using System.Text.RegularExpressions;
using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.LlmPlanning;

namespace Norn.Planner.Validation;

/// <summary>Plano já validado, pronto para virar <see cref="HealingPlan"/>.</summary>
public sealed record ValidatedLlmPlan(string Rationale, double Confidence, IReadOnlyList<HealingAction> Actions, string ExpectedOutcome);

/// <summary>
/// Vocabulário fixo de <c>llmTrace.failureReasons</c> (§5.5) — dado experimental, não apenas
/// robustez: a taxa de fallback decomposta por motivo é resultado de primeira linha (§3).
/// </summary>
public static class LlmFailureReasons
{
    public const string InvalidJson = "InvalidJson";
    public const string ActionNotInCatalog = "ActionNotInCatalog";
    public const string PreconditionViolation = "PreconditionViolation";
    public const string Timeout = "Timeout";
    public const string PromptBudgetExceeded = "PromptBudgetExceeded";

    /// <summary>
    /// Extensão pragmática ao vocabulário fechado do §5.5: falha de conexão com o Ollama (servidor
    /// fora do ar, recusa de conexão) não é o mesmo modo de falha que estourar o timeout de uma
    /// chamada que respondeu devagar — decompor os dois preserva a mesma granularidade de análise
    /// que o plano já pede para Timeout × InvalidJson.
    /// </summary>
    public const string ConnectorError = "ConnectorError";
}

public sealed record LlmValidationResult
{
    public required bool Success { get; init; }

    public ValidatedLlmPlan? Plan { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>Detalhe enviado de volta ao modelo na tentativa de reparo (§5.5, passo 5).</summary>
    public string? ErrorDetail { get; init; }

    public static LlmValidationResult Ok(ValidatedLlmPlan plan) => new() { Success = true, Plan = plan };

    public static LlmValidationResult Fail(string reason, string detail) =>
        new() { Success = false, FailureReason = reason, ErrorDetail = detail };
}

/// <summary>
/// Pipeline de validação do §5.5, passos 1–4: extrai JSON (tolera cercas markdown), desserializa,
/// valida <c>action.type</c> contra o enum fechado e cada ação contra a tabela de pré-condições do
/// §5.4 (via <see cref="HealingActionPreconditionChecker"/> — a mesma usada pelo <c>RuleEngine</c>,
/// tarefa 6). Nunca chama o Ollama; recebe só o texto já retornado pelo conector.
/// </summary>
public sealed partial class LlmOutputValidator(HealingActionPreconditionChecker preconditionChecker)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public LlmValidationResult Validate(string rawResponse, AnomalyContext context)
    {
        var jsonText = ExtractJson(rawResponse);

        LlmPlanResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<LlmPlanResponseDto>(jsonText, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return LlmValidationResult.Fail(LlmFailureReasons.InvalidJson, ex.Message);
        }

        if (dto is null)
        {
            return LlmValidationResult.Fail(LlmFailureReasons.InvalidJson, "Resposta desserializou para null.");
        }

        var actions = new List<HealingAction>();
        var order = 0;
        foreach (var actionDto in dto.Actions)
        {
            if (actionDto.Type is null || !Enum.TryParse<HealingActionType>(actionDto.Type, ignoreCase: true, out var type))
            {
                return LlmValidationResult.Fail(
                    LlmFailureReasons.ActionNotInCatalog,
                    $"Ação '{actionDto.Type}' não está no catálogo fechado ({string.Join(", ", Enum.GetNames<HealingActionType>())}).");
            }

            var action = new HealingAction
            {
                ActionId = Guid.NewGuid(),
                Type = type,
                Target = context.PrimarySignal.Target,
                Parameters = NormalizeParameters(actionDto.Parameters),
                Order = order++,
            };

            var precondition = preconditionChecker.Check(context, action);
            if (!precondition.Accepted)
            {
                return LlmValidationResult.Fail(LlmFailureReasons.PreconditionViolation, precondition.RejectionReason ?? "Pré-condição recusada.");
            }

            actions.Add(action);
        }

        return LlmValidationResult.Ok(new ValidatedLlmPlan(
            dto.Rationale ?? string.Empty,
            dto.Confidence,
            actions,
            dto.ExpectedOutcome ?? string.Empty));
    }

    /// <summary>
    /// O contrato de <see cref="HealingAction.Parameters"/> é <c>string</c>; o modelo às vezes
    /// manda número ou booleano JSON puro (ex.: <c>replicaDelta</c> sem aspas). Normaliza pelo
    /// <see cref="JsonValueKind"/> real em vez de assumir string, para não confundir "o modelo
    /// respondeu tipo errado" com <see cref="LlmFailureReasons.InvalidJson"/> — os dois são modos
    /// de falha distintos, e só o primeiro é falha de verdade.
    /// </summary>
    private static Dictionary<string, string> NormalizeParameters(Dictionary<string, JsonElement> rawParameters)
    {
        var normalized = new Dictionary<string, string>(rawParameters.Count);
        foreach (var (key, value) in rawParameters)
        {
            normalized[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.True or JsonValueKind.False => value.GetBoolean().ToString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Null => string.Empty,
                _ => value.GetRawText(),
            };
        }

        return normalized;
    }

    /// <summary>Tolera cerca markdown (```` ```json ... ``` ````) — o resto do texto é o JSON como está.</summary>
    private static string ExtractJson(string rawResponse)
    {
        var trimmed = rawResponse.Trim();
        var match = MarkdownFenceRegex().Match(trimmed);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    [GeneratedRegex(@"^```(?:json)?\s*\n?(.*?)\n?```$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownFenceRegex();
}
