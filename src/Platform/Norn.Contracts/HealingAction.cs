namespace Norn.Contracts;

public sealed record HealingAction
{
    public required Guid ActionId { get; init; }

    public required HealingActionType Type { get; init; }

    public required ServiceTarget Target { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public required int Order { get; init; }

    public bool DryRun { get; init; }
}
