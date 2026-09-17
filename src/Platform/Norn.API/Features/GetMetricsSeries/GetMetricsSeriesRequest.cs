namespace Norn.API.Features.GetMetricsSeries;

public sealed record GetMetricsSeriesRequest
{
    public required string MetricName { get; init; }

    public required string Service { get; init; }

    public required DateTimeOffset FromUtc { get; init; }

    public required DateTimeOffset ToUtc { get; init; }
}
