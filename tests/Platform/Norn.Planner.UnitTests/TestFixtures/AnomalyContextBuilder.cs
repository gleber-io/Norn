using System.Globalization;
using Norn.Contracts;

namespace Norn.Planner.UnitTests.TestFixtures;

/// <summary>
/// Monta um <see cref="AnomalyContext"/> mínimo e válido para os testes do Planner, com os campos
/// mais usados como parâmetro opcional. Não é teste — é fixture compartilhada.
/// </summary>
internal static class AnomalyContextBuilder
{
    public static readonly DateTimeOffset DefaultCreatedAtUtc = DateTimeOffset.Parse("2026-09-15T12:00:00Z", CultureInfo.InvariantCulture);

    public static AnomalyContext Build(
        string primaryMetricName = "dotnet_process_memory_working_set_bytes",
        Severity severity = Severity.High,
        IReadOnlyList<AnomalySignal>? correlatedSignals = null,
        int currentReplicas = 1,
        string? pod = "catalog-api-abc123",
        string? podUid = "uid-1",
        IReadOnlyList<HealingHistoryEntry>? healingHistory = null,
        bool inCooldown = false,
        DateTimeOffset? createdAtUtc = null)
    {
        var created = createdAtUtc ?? DefaultCreatedAtUtc;
        var target = new ServiceTarget
        {
            Service = "Norn.Shop.Catalog.API",
            Namespace = "norn-shop",
            Pod = pod,
            PodUid = podUid,
        };

        return new AnomalyContext
        {
            ContextId = Guid.NewGuid(),
            CreatedAtUtc = created,
            CorrelationId = Guid.NewGuid(),
            PrimarySignal = Signal(primaryMetricName, severity, target, created),
            CorrelatedSignals = correlatedSignals ?? [],
            Topology = new TopologyInfo
            {
                Service = target.Service,
                CurrentReplicas = currentReplicas,
                DesiredReplicas = currentReplicas,
                ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
                ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
            },
            RecentMetrics = new RecentMetrics
            {
                CpuUtilizationPct = 10,
                MemoryWorkingSetBytes = 200_000_000,
                RequestRatePerSecond = 5,
                ErrorRatePct = 0,
                LatencyP99Ms = 100,
                QueueDepth = 0,
            },
            HealingHistory = healingHistory ?? [],
            CooldownStatus = new CooldownStatus { IsInCooldown = inCooldown },
        };
    }

    public static AnomalySignal Correlated(string metricName, Severity severity = Severity.Medium) =>
        Signal(metricName, severity, new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" }, DefaultCreatedAtUtc);

    private static AnomalySignal Signal(string metricName, Severity severity, ServiceTarget target, DateTimeOffset detectedAtUtc) => new()
    {
        SignalId = Guid.NewGuid(),
        DetectedAtUtc = detectedAtUtc,
        Target = target,
        MetricName = metricName,
        Detector = DetectorType.SpikeDetection,
        Severity = severity,
        Confidence = 95.0,
        PValue = 0.01,
        ObservedValue = 1,
        ExpectedValue = 0,
        Window = new TimeWindow { FromUtc = detectedAtUtc.AddMinutes(-1), ToUtc = detectedAtUtc },
    };
}
