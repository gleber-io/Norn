namespace Norn.Contracts;

/// <summary>
/// <c>Rejected</c> só existe depois de um plano — recusa de pré-condição no Planner produz
/// <c>NoOp</c>, nunca este status (§5.4).
/// </summary>
public enum HealingOutcomeStatus
{
    Succeeded,
    Failed,
    PartiallyApplied,
    Rejected,
    TimedOut,
}

/// <summary>Verificação pós-atuação (§5.3).</summary>
public sealed record HealingOutcome
{
    public required Guid OutcomeId { get; init; }

    public required Guid PlanId { get; init; }

    public required DateTimeOffset AppliedAtUtc { get; init; }

    public required DateTimeOffset VerifiedAtUtc { get; init; }

    public required HealingOutcomeStatus Status { get; init; }

    public required bool SloRestored { get; init; }

    public required double TimeToRecoverySeconds { get; init; }

    public required RecentMetrics MetricsBefore { get; init; }

    public required RecentMetrics MetricsAfter { get; init; }

    public string? ErrorMessage { get; init; }
}
