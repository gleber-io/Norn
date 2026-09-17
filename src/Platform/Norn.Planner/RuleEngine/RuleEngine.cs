using System.Globalization;
using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.Settings;

namespace Norn.Planner.RuleEngine;

/// <summary>
/// Braço C e fallback do braço B (§5.5, tarefa 1). <see cref="DecideActionType"/> é a função pura
/// e total exigida pela tarefa 9a — sem relógio, sem Redis, sem cooldown embutido, testável
/// exaustivamente sobre os 2^7 subconjuntos da assinatura fechada (docs/metrics-matrix.md). As
/// barreiras do ADR-04 e as pré-condições do §5.4 vivem fora dela, em
/// <see cref="HealingActionPreconditionChecker"/> (tarefa 6) — <see cref="Decide"/> é a casca que
/// aplica as duas sobre o resultado da função pura e monta o <see cref="HealingPlan"/> final. Essa
/// casca não é pura (usa <see cref="TimeProvider"/> só para carimbar <c>CreatedAtUtc</c>), mas a
/// pureza exigida pelo plano é a do núcleo de decisão, não do envelope do plano.
/// </summary>
public sealed class RuleEngine(
    PlannerOptions options,
    HealingActionPreconditionChecker preconditionChecker,
    TimeProvider timeProvider)
{
    // Assinatura fechada M = 7 (docs/metrics-matrix.md, seção 1) — mesmos literais de
    // Norn.Analyzer.Settings.SeverityBandOptions e Norn.Monitor.Prometheus.PrometheusQueryCatalog.
    private const string Rss = "dotnet_process_memory_working_set_bytes";
    private const string GcPause = "dotnet_gc_pause_time_seconds_total";
    private const string LatencyP99 = "http_server_request_duration_seconds_bucket";
    private const string QueueDepth = "rabbitmq_queue_messages_ready";
    private const string GatewayLatency = "norn_shop_payments_gateway_latency_ms";
    private const string ErrorRate5xx = "http_server_request_duration_seconds_count";
    private const string ErrorsByType = "norn_app_errors_total";

    private static readonly IReadOnlySet<string> F1Signature = new HashSet<string> { Rss, GcPause };
    private static readonly IReadOnlySet<string> F2Signature = new HashSet<string> { LatencyP99, QueueDepth };
    private static readonly IReadOnlySet<string> F3Signature = new HashSet<string> { GatewayLatency, ErrorRate5xx };

    /// <summary>Todas as métricas da assinatura fechada — usado pelos testes exaustivos (tarefa 9a) para gerar os 2^7 subconjuntos.</summary>
    public static IReadOnlyList<string> SignatureMetrics { get; } = [Rss, GcPause, LatencyP99, QueueDepth, GatewayLatency, ErrorRate5xx, ErrorsByType];

    public sealed record Decision(HealingActionType ActionType, string Rationale);

    /// <summary>
    /// Domínio: (conjunto de métricas alteradas do contexto, banda de severidade do primário) →
    /// ação. Total sobre os 2^7 subconjuntos — nunca lança, nunca cai em branch default silencioso.
    /// Prioridade determinística, documentada em <c>docs/rule-table.md</c>: F1 &gt; F2 &gt; F3 &gt;
    /// NoOp. <c>RestartPod</c> é a única ação irreversível do catálogo, então vence qualquer
    /// ambiguidade de assinatura antes de ações reversíveis (F2/F3); severidade abaixo de
    /// <see cref="Severity.Medium"/> nunca aciona — é o que dá conteúdo à fronteira da tarefa 9b.
    /// </summary>
    public static Decision DecideActionType(IReadOnlySet<string> alteredMetrics, Severity severityBand)
    {
        if (severityBand == Severity.Low)
        {
            return new Decision(HealingActionType.NoOp, "Severidade Low — RuleEngine não age abaixo de Medium.");
        }

        if (alteredMetrics.Overlaps(F1Signature))
        {
            return new Decision(HealingActionType.RestartPod, "Assinatura de F1 (RSS/tempo de GC) presente — reinicia o pod alvo.");
        }

        if (alteredMetrics.Overlaps(F2Signature))
        {
            return new Decision(HealingActionType.ScaleUp, "Assinatura de F2 (latência p99/profundidade de fila) presente — aumenta réplicas.");
        }

        if (alteredMetrics.Overlaps(F3Signature))
        {
            return new Decision(HealingActionType.ToggleFeatureFlag, "Assinatura de F3 (latência do gateway/taxa de 5xx) presente — ativa o bypass do gateway.");
        }

        return new Decision(HealingActionType.NoOp, "Nenhuma assinatura de cenário conhecida no conjunto de métricas alteradas — abstenção.");
    }

    /// <summary>
    /// Aplica <see cref="DecideActionType"/> sobre o contexto real, preenche os parâmetros
    /// concretos da ação a partir dele, roda a barreira/pré-condição e monta o plano final.
    /// <c>LlmTrace</c> sai vazio aqui — quando usado como fallback do braço B (§5.5, passo 6), o
    /// chamador substitui pelo trace real da tentativa de LLM (o plano é imutável; troca-se com
    /// <c>with</c>).
    /// </summary>
    public HealingPlan Decide(AnomalyContext context)
    {
        var alteredMetrics = new HashSet<string>(context.CorrelatedSignals.Select(s => s.MetricName))
        {
            context.PrimarySignal.MetricName,
        };

        var decision = DecideActionType(alteredMetrics, context.PrimarySignal.Severity);
        var candidateAction = BuildCandidateAction(decision.ActionType, context);

        var precondition = candidateAction is null
            ? PreconditionResult.Ok()
            : preconditionChecker.Check(context, candidateAction);

        var accepted = candidateAction is not null && precondition.Accepted;
        var decidedBy = accepted ? DecidedBy.RuleEngine : DecidedBy.Fallback;
        var rationale = accepted
            ? decision.Rationale
            : CombineRationale(decision.Rationale, precondition.RejectionReason);

        var finalAction = accepted ? candidateAction! : BuildNoOpAction(context, rationale);

        return new HealingPlan
        {
            PlanId = Guid.NewGuid(),
            ContextId = context.ContextId,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            DecidedBy = decidedBy,
            Rationale = Truncate(rationale, 500),
            Confidence = accepted ? 100.0 : 0.0,
            Actions = [finalAction],
            ExpectedOutcome = accepted
                ? $"SLO restaurado após {finalAction.Type} sobre {finalAction.Target.Service}."
                : "Nenhuma ação segura disponível — sistema permanece no estado atual.",
            VerificationWindowSeconds = options.VerificationWindowSecondsFor(finalAction.Type),
            LlmTrace = new LlmTrace(),
        };
    }

    private HealingAction? BuildCandidateAction(HealingActionType type, AnomalyContext context) => type switch
    {
        HealingActionType.NoOp => null,
        HealingActionType.ScaleUp => new HealingAction
        {
            ActionId = Guid.NewGuid(),
            Type = HealingActionType.ScaleUp,
            Target = context.PrimarySignal.Target,
            Parameters = new Dictionary<string, string>
            {
                ["replicaDelta"] = options.DefaultReplicaDelta.ToString(CultureInfo.InvariantCulture),
            },
            Order = 0,
        },
        HealingActionType.RestartPod => new HealingAction
        {
            ActionId = Guid.NewGuid(),
            Type = HealingActionType.RestartPod,
            Target = context.PrimarySignal.Target,
            Parameters = new Dictionary<string, string>
            {
                ["podName"] = context.PrimarySignal.Target.Pod ?? string.Empty,
                ["podUid"] = context.PrimarySignal.Target.PodUid ?? string.Empty,
            },
            Order = 0,
        },
        HealingActionType.ToggleFeatureFlag => new HealingAction
        {
            ActionId = Guid.NewGuid(),
            Type = HealingActionType.ToggleFeatureFlag,
            Target = context.PrimarySignal.Target,
            Parameters = new Dictionary<string, string>
            {
                ["flagName"] = ShopFlagCatalog.PaymentGatewayBypass,
                ["value"] = "true",
            },
            Order = 0,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de ação fora do catálogo fechado."),
    };

    private static HealingAction BuildNoOpAction(AnomalyContext context, string reason) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.NoOp,
        Target = context.PrimarySignal.Target,
        Parameters = new Dictionary<string, string> { ["reason"] = Truncate(reason, 500) },
        Order = 0,
    };

    private static string CombineRationale(string decisionRationale, string? rejectionReason) =>
        rejectionReason is null ? decisionRationale : $"{decisionRationale} Recusado: {rejectionReason}";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
