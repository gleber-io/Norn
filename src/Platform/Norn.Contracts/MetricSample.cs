namespace Norn.Contracts;

/// <summary>Unidade bruta vinda do Monitor (§5.3).</summary>
public sealed record MetricSample
{
    public required string MetricName { get; init; }

    public required ServiceTarget Target { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required double Value { get; init; }

    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}
