using Norn.Contracts;
using Norn.PairedAnalysis;
using Shouldly;
using Xunit;

namespace Norn.PairedAnalysis.UnitTests;

/// <summary>
/// Fase 12, tarefa 4a: o recálculo pareado compara o que o LLM decidiu de fato (via HealingPlan)
/// contra o que a mesma entrada teria produzido no RuleEngine (função pura), e os dois contra o
/// gabarito do cenário (§3). Fixtures usam os nomes de métrica literais da assinatura fechada M=7
/// (Norn.Planner.RuleEngine.RuleEngine), para exercitar os branches reais, não só a superfície.
/// </summary>
public sealed class PairedDecisionCalculatorTests
{
    private const string Rss = "dotnet_process_memory_working_set_bytes";
    private const string GatewayLatency = "norn_shop_payments_gateway_latency_ms";

    [Fact]
    public void Calculate_F1SignatureAndLlmAgreesWithReference_BothMatchReference()
    {
        var context = ContextWithPrimaryMetric(Rss, Severity.Critical);

        var row = PairedDecisionCalculator.Calculate(Guid.NewGuid(), "F1", context, HealingActionType.RestartPod);

        row.RuleAction.ShouldBe(HealingActionType.RestartPod);
        row.ReferenceAction.ShouldBe(HealingActionType.RestartPod);
        row.LlmMatchesReference.ShouldBe(1);
        row.RuleMatchesReference.ShouldBe(1);
    }

    [Fact]
    public void Calculate_F3AmbiguousSignature_LlmNoOpDivergesFromRuleAndReference()
    {
        // Achado real de docs/experiments/estabilidade-llm.md: sobre a mesma assinatura de F3, o
        // LLM escolhe NoOp e o RuleEngine escolhe ToggleFeatureFlag, de forma estável — os dois
        // decisores divergem, e só a regra bate com a ação de referência do cenário.
        var context = ContextWithPrimaryMetric(GatewayLatency, Severity.Critical);

        var row = PairedDecisionCalculator.Calculate(Guid.NewGuid(), "F3", context, HealingActionType.NoOp);

        row.RuleAction.ShouldBe(HealingActionType.ToggleFeatureFlag);
        row.ReferenceAction.ShouldBe(HealingActionType.ToggleFeatureFlag);
        row.LlmMatchesReference.ShouldBe(0);
        row.RuleMatchesReference.ShouldBe(1);
    }

    [Fact]
    public void Calculate_LowSeverity_RuleAbstainsRegardlessOfSignature()
    {
        var context = ContextWithPrimaryMetric(Rss, Severity.Low);

        var row = PairedDecisionCalculator.Calculate(Guid.NewGuid(), "F1", context, HealingActionType.RestartPod);

        row.RuleAction.ShouldBe(HealingActionType.NoOp);
        row.RuleMatchesReference.ShouldBe(0);
    }

    [Fact]
    public void Calculate_CorrelatedSignalCarriesSignature_StillDetectedEvenWhenPrimaryIsUnrelated()
    {
        // ADR-14: o primário é ordem de leitura, não hipótese de causa raiz — a assinatura pode vir
        // de um sinal correlacionado, não só do primário.
        var context = ContextWithPrimaryMetric("norn_app_errors_total", Severity.Critical) with
        {
            CorrelatedSignals = [Signal(Rss, Severity.Medium)],
        };

        var row = PairedDecisionCalculator.Calculate(Guid.NewGuid(), "F1", context, HealingActionType.RestartPod);

        row.RuleAction.ShouldBe(HealingActionType.RestartPod);
    }

    [Fact]
    public void ScenarioReferenceActions_UnknownScenario_Throws()
    {
        Should.Throw<ArgumentException>(() => ScenarioReferenceActions.For("F99"));
    }

    private static AnomalyContext ContextWithPrimaryMetric(string metricName, Severity severity) => new()
    {
        ContextId = Guid.NewGuid(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid(),
        PrimarySignal = Signal(metricName, severity),
        Topology = new TopologyInfo
        {
            Service = "Norn.Shop.Catalog.API",
            CurrentReplicas = 1,
            DesiredReplicas = 1,
            ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
            ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
        },
        RecentMetrics = new RecentMetrics
        {
            CpuUtilizationPct = 10,
            MemoryWorkingSetBytes = 500_000_000,
            RequestRatePerSecond = 5,
            ErrorRatePct = 0,
            LatencyP99Ms = 50,
            QueueDepth = 0,
        },
        CooldownStatus = new CooldownStatus { IsInCooldown = false },
    };

    private static AnomalySignal Signal(string metricName, Severity severity) => new()
    {
        SignalId = Guid.NewGuid(),
        DetectedAtUtc = DateTimeOffset.UtcNow,
        Target = new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" },
        MetricName = metricName,
        Detector = DetectorType.SpikeDetection,
        Severity = severity,
        Confidence = 95,
        PValue = 0.001,
        ObservedValue = 1,
        ExpectedValue = 0,
        Window = new TimeWindow { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1), ToUtc = DateTimeOffset.UtcNow },
    };
}
