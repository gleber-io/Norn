namespace Norn.Contracts;

/// <summary>
/// Entrada do Planner — o Monitor enriquece o sinal (§5.3). Persistido inteiro em
/// <c>anomaly_contexts</c>, com <c>contextHash</c> canônico, antes de chegar ao Planner,
/// em todos os modos (Norn.Knowledge, Fase 7).
/// </summary>
public sealed record AnomalyContext
{
    public required Guid ContextId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }

    public Guid? ExperimentRunId { get; init; }

    public required AnomalySignal PrimarySignal { get; init; }

    public IReadOnlyList<AnomalySignal> CorrelatedSignals { get; init; } = [];

    public required TopologyInfo Topology { get; init; }

    public required RecentMetrics RecentMetrics { get; init; }

    public IReadOnlyList<HealingHistoryEntry> HealingHistory { get; init; } = [];

    public IReadOnlyDictionary<string, bool> ActiveFeatureFlags { get; init; } = new Dictionary<string, bool>();

    public required CooldownStatus CooldownStatus { get; init; }
}

public sealed record TopologyInfo
{
    public required string Service { get; init; }

    public required int CurrentReplicas { get; init; }

    public required int DesiredReplicas { get; init; }

    public required ResourceSpec ResourceRequests { get; init; }

    public required ResourceSpec ResourceLimits { get; init; }

    public IReadOnlyList<string> DependsOn { get; init; } = [];

    public IReadOnlyList<string> DependedOnBy { get; init; } = [];
}

public sealed record ResourceSpec
{
    public required string Cpu { get; init; }

    public required string Memory { get; init; }
}

public sealed record RecentMetrics
{
    public required double CpuUtilizationPct { get; init; }

    public required long MemoryWorkingSetBytes { get; init; }

    public required double RequestRatePerSecond { get; init; }

    public required double ErrorRatePct { get; init; }

    public required double LatencyP99Ms { get; init; }

    public required int QueueDepth { get; init; }

    public IReadOnlyDictionary<string, int> ErrorsByType { get; init; } = new Dictionary<string, int>();
}

public sealed record HealingHistoryEntry
{
    public required string Action { get; init; }

    public required DateTimeOffset AppliedAtUtc { get; init; }

    public required string Outcome { get; init; }
}

public sealed record CooldownStatus
{
    public required bool IsInCooldown { get; init; }

    public DateTimeOffset? CooldownUntilUtc { get; init; }
}
