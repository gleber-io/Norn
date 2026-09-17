using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetMetricsSeries;

public static class GetMetricsSeriesEndpoint
{
    public static RouteGroupBuilder MapGetMetricsSeries(this RouteGroupBuilder group)
    {
        group.MapGet("/metrics/series", async Task<Ok<IReadOnlyList<MetricSample>>> (
                [AsParameters] GetMetricsSeriesRequest request,
                IMetricSource metricSource,
                CancellationToken cancellationToken) =>
            {
                var response = await GetMetricsSeriesHandler.HandleAsync(request, metricSource, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<GetMetricsSeriesRequest>()
            .WithName("GetMetricsSeries");

        return group;
    }
}
