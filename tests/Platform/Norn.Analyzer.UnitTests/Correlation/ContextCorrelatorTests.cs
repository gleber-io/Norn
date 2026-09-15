using Norn.Analyzer.Correlation;
using Norn.Contracts;
using Norn.Contracts.Serialization;
using Shouldly;
using Xunit;

namespace Norn.Analyzer.UnitTests.Correlation;

public sealed class ContextCorrelatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OrderDeterministically_MixedSeverities_OrdersBySeverityDescendingFirst()
    {
        var low = Signal(Severity.Low, Now, confidence: 95, metricName: "b_metric");
        var critical = Signal(Severity.Critical, Now, confidence: 95, metricName: "a_metric");
        var medium = Signal(Severity.Medium, Now, confidence: 95, metricName: "c_metric");

        var ordered = ContextCorrelator.OrderDeterministically([low, critical, medium]);

        ordered.ShouldBe([critical, medium, low]);
    }

    [Fact]
    public void OrderDeterministically_SameSeverity_BreaksTiesByDetectedAtUtcAscending()
    {
        var later = Signal(Severity.High, Now.AddSeconds(10), confidence: 95, metricName: "a_metric");
        var earlier = Signal(Severity.High, Now, confidence: 95, metricName: "b_metric");

        var ordered = ContextCorrelator.OrderDeterministically([later, earlier]);

        ordered.ShouldBe([earlier, later]);
    }

    [Fact]
    public void OrderDeterministically_SameSeverityAndTimestamp_BreaksTiesByConfidenceDescending()
    {
        var lowConfidence = Signal(Severity.High, Now, confidence: 60, metricName: "a_metric");
        var highConfidence = Signal(Severity.High, Now, confidence: 99, metricName: "b_metric");

        var ordered = ContextCorrelator.OrderDeterministically([lowConfidence, highConfidence]);

        ordered.ShouldBe([highConfidence, lowConfidence]);
    }

    [Fact]
    public void OrderDeterministically_FullTie_BreaksByMetricNameOrdinalAscending()
    {
        var zMetric = Signal(Severity.High, Now, confidence: 95, metricName: "z_metric");
        var aMetric = Signal(Severity.High, Now, confidence: 95, metricName: "a_metric");

        var ordered = ContextCorrelator.OrderDeterministically([zMetric, aMetric]);

        ordered.ShouldBe([aMetric, zMetric]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BuildContext_SameSignalSetInAnyArrivalOrder_ProducesSameContextHashAndPrimarySignal(int rotation)
    {
        var signals = new[]
        {
            Signal(Severity.Critical, Now, confidence: 95, metricName: "a_metric"),
            Signal(Severity.Medium, Now, confidence: 95, metricName: "b_metric"),
            Signal(Severity.Low, Now, confidence: 95, metricName: "c_metric"),
        };
        var rotated = signals.Skip(rotation).Concat(signals.Take(rotation)).ToArray();

        var baseline = BuildFixedContext(signals);
        var underTest = BuildFixedContext(rotated);

        CanonicalJson.ComputeHash(underTest).ShouldBe(CanonicalJson.ComputeHash(baseline));
        underTest.PrimarySignal.ShouldBe(baseline.PrimarySignal);
    }

    [Fact]
    public void BuildContext_EmptySignalList_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => ContextCorrelator.BuildContext(
            [],
            contextId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            experimentRunId: null,
            createdAtUtc: Now,
            topology: Topology(),
            recentMetrics: RecentMetrics(),
            healingHistory: [],
            activeFeatureFlags: new Dictionary<string, bool>(),
            cooldownStatus: new CooldownStatus { IsInCooldown = false }));
    }

    private static AnomalyContext BuildFixedContext(IReadOnlyList<AnomalySignal> signals) =>
        ContextCorrelator.BuildContext(
            signals,
            contextId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            correlationId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            experimentRunId: null,
            createdAtUtc: Now,
            topology: Topology(),
            recentMetrics: RecentMetrics(),
            healingHistory: [],
            activeFeatureFlags: new Dictionary<string, bool> { ["payment.gateway.bypass"] = false },
            cooldownStatus: new CooldownStatus { IsInCooldown = false });

    private static TopologyInfo Topology() => new()
    {
        Service = "Norn.Shop.Catalog.API",
        CurrentReplicas = 1,
        DesiredReplicas = 1,
        ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
        ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
    };

    private static RecentMetrics RecentMetrics() => new()
    {
        CpuUtilizationPct = 10,
        MemoryWorkingSetBytes = 200_000_000,
        RequestRatePerSecond = 5,
        ErrorRatePct = 0,
        LatencyP99Ms = 50,
        QueueDepth = 0,
    };

    private static AnomalySignal Signal(Severity severity, DateTimeOffset detectedAtUtc, double confidence, string metricName) => new()
    {
        SignalId = Guid.NewGuid(),
        DetectedAtUtc = detectedAtUtc,
        Target = new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" },
        MetricName = metricName,
        Detector = DetectorType.SpikeDetection,
        Severity = severity,
        Confidence = confidence,
        PValue = 0.01,
        ObservedValue = 1,
        ExpectedValue = 0,
        Window = new TimeWindow { FromUtc = detectedAtUtc.AddMinutes(-1), ToUtc = detectedAtUtc },
    };
}
