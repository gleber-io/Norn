using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetMetricsSeries;

public static class GetMetricsSeriesHandler
{
    public static Task<IReadOnlyList<MetricSample>> HandleAsync(
        GetMetricsSeriesRequest request, IMetricSource metricSource, CancellationToken cancellationToken)
    {
        var promQlQuery = $"{request.MetricName}{{exported_job=\"{request.Service}\"}}";
        return metricSource.QueryRangeAsync(promQlQuery, request.FromUtc, request.ToUtc, cancellationToken);
    }
}
