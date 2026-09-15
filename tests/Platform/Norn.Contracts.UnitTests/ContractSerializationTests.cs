using Xunit;

namespace Norn.Contracts.UnitTests;

public sealed class ContractSerializationTests
{
    private static readonly ServiceTarget Target = new()
    {
        Service = "catalog",
        Namespace = "norn-shop",
        Pod = "catalog-7f8d9-abcde",
        PodUid = "d290f1ee-6c54-4b01-90e6-d701748f0851",
    };

    private static readonly RecentMetrics Metrics = new()
    {
        CpuUtilizationPct = 72.5,
        MemoryWorkingSetBytes = 314_572_800,
        RequestRatePerSecond = 12.3,
        ErrorRatePct = 1.8,
        LatencyP99Ms = 245.0,
        QueueDepth = 4,
        ErrorsByType = new Dictionary<string, int> { ["System.TimeoutException"] = 3 },
    };

    [Fact]
    public void MetricSample_Should_RoundTrip_ThroughJson()
    {
        var sample = new MetricSample
        {
            MetricName = "dotnet.process.memory.working_set",
            Target = Target,
            TimestampUtc = DateTimeOffset.UtcNow,
            Value = 314_572_800,
            Labels = new Dictionary<string, string> { ["service"] = "catalog" },
        };

        JsonRoundTripAssert.RoundTrips(sample);
    }

    [Fact]
    public void AnomalySignal_Should_RoundTrip_ThroughJson()
    {
        var signal = new AnomalySignal
        {
            SignalId = Guid.NewGuid(),
            DetectedAtUtc = DateTimeOffset.UtcNow,
            Target = Target,
            MetricName = "dotnet.process.memory.working_set",
            Detector = DetectorType.SpikeDetection,
            Severity = Severity.High,
            Confidence = 87.5,
            PValue = 0.01,
            ObservedValue = 314_572_800,
            ExpectedValue = 150_000_000,
            Window = new TimeWindow { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-5), ToUtc = DateTimeOffset.UtcNow },
            ExperimentRunId = Guid.NewGuid(),
        };

        JsonRoundTripAssert.RoundTrips(signal);
    }

    [Fact]
    public void AnomalyContext_Should_RoundTrip_ThroughJson()
    {
        var primarySignal = new AnomalySignal
        {
            SignalId = Guid.NewGuid(),
            DetectedAtUtc = DateTimeOffset.UtcNow,
            Target = Target,
            MetricName = "dotnet.process.memory.working_set",
            Detector = DetectorType.SpikeDetection,
            Severity = Severity.Critical,
            Confidence = 92.0,
            PValue = 0.001,
            ObservedValue = 500_000_000,
            ExpectedValue = 150_000_000,
            Window = new TimeWindow { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-5), ToUtc = DateTimeOffset.UtcNow },
        };

        var context = new AnomalyContext
        {
            ContextId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            ExperimentRunId = Guid.NewGuid(),
            PrimarySignal = primarySignal,
            CorrelatedSignals = [primarySignal],
            Topology = new TopologyInfo
            {
                Service = "catalog",
                CurrentReplicas = 1,
                DesiredReplicas = 1,
                ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
                ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
                DependsOn = ["postgres"],
                DependedOnBy = ["order"],
            },
            RecentMetrics = Metrics,
            HealingHistory =
            [
                new HealingHistoryEntry
                {
                    Action = nameof(HealingActionType.RestartPod),
                    AppliedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                    Outcome = nameof(HealingOutcomeStatus.Succeeded),
                },
            ],
            ActiveFeatureFlags = new Dictionary<string, bool> { ["payment.gateway.bypass"] = false },
            CooldownStatus = new CooldownStatus { IsInCooldown = false },
        };

        JsonRoundTripAssert.RoundTrips(context);
    }

    [Fact]
    public void HealingPlan_Should_RoundTrip_ThroughJson()
    {
        var plan = new HealingPlan
        {
            PlanId = Guid.NewGuid(),
            ContextId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            DecidedBy = DecidedBy.Llm,
            Rationale = "Working set acima do esperado; reiniciar o pod.",
            Confidence = 0.8,
            Actions =
            [
                new HealingAction
                {
                    ActionId = Guid.NewGuid(),
                    Type = HealingActionType.RestartPod,
                    Target = Target,
                    Parameters = new Dictionary<string, string> { ["podName"] = "catalog-7f8d9-abcde" },
                    Order = 1,
                },
            ],
            ExpectedOutcome = "Working set volta ao baseline em até 120s.",
            LlmTrace = new LlmTrace
            {
                PromptHash = "sha256:abc123",
                Model = "norn-qwen",
                LatencyMs = 850,
                Attempts = 1,
                FailureReasons = [],
            },
        };

        JsonRoundTripAssert.RoundTrips(plan);
    }

    [Fact]
    public void HealingOutcome_Should_RoundTrip_ThroughJson()
    {
        var outcome = new HealingOutcome
        {
            OutcomeId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            AppliedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-30),
            VerifiedAtUtc = DateTimeOffset.UtcNow,
            Status = HealingOutcomeStatus.Succeeded,
            SloRestored = true,
            TimeToRecoverySeconds = 28.4,
            MetricsBefore = Metrics,
            MetricsAfter = Metrics with { MemoryWorkingSetBytes = 160_000_000 },
            ErrorMessage = null,
        };

        JsonRoundTripAssert.RoundTrips(outcome);
    }
}
