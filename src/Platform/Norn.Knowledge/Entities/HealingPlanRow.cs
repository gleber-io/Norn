namespace Norn.Knowledge.Entities;

/// <summary><c>healing_plans</c> (tarefa 1 da Fase 7; consumida a partir da Fase 8).</summary>
public sealed class HealingPlanRow
{
    public required Guid PlanId { get; init; }

    public required Guid ContextId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string DecidedBy { get; init; }

    public required string Payload { get; init; }
}
