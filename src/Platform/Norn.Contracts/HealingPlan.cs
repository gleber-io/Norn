namespace Norn.Contracts;

public enum DecidedBy
{
    Llm,
    RuleEngine,
    Fallback,
}

/// <summary>Saída do Planner (§5.3, §5.5).</summary>
public sealed record HealingPlan
{
    public required Guid PlanId { get; init; }

    public required Guid ContextId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DecidedBy DecidedBy { get; init; }

    /// <summary>Máx. 500 caracteres.</summary>
    public required string Rationale { get; init; }

    public required double Confidence { get; init; }

    public IReadOnlyList<HealingAction> Actions { get; init; } = [];

    public required string ExpectedOutcome { get; init; }

    public int VerificationWindowSeconds { get; init; } = 120;

    public required LlmTrace LlmTrace { get; init; }
}

/// <summary>
/// Todo motivo de falha registrado aqui é dado experimental (§5.5): a taxa de fallback
/// decomposta por motivo é resultado de primeira linha (§3).
/// </summary>
public sealed record LlmTrace
{
    public string? PromptHash { get; init; }

    public string? Model { get; init; }

    public long LatencyMs { get; init; }

    public int Attempts { get; init; } = 1;

    public IReadOnlyList<string> FailureReasons { get; init; } = [];
}
