namespace Norn.Knowledge.Entities;

/// <summary><c>healing_outcomes</c> (tarefa 1 da Fase 7; consumida a partir da Fase 9).</summary>
public sealed class HealingOutcomeRow
{
    public required Guid OutcomeId { get; init; }

    public required Guid PlanId { get; init; }

    public required DateTimeOffset AppliedAtUtc { get; init; }

    public required DateTimeOffset VerifiedAtUtc { get; init; }

    public required string Status { get; init; }

    public required string Payload { get; init; }
}
