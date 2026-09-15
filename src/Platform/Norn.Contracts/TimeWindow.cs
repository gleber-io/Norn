namespace Norn.Contracts;

public sealed record TimeWindow
{
    public required DateTimeOffset FromUtc { get; init; }

    public required DateTimeOffset ToUtc { get; init; }
}
