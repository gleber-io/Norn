using Microsoft.AspNetCore.Http.HttpResults;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetExperimentRun;

public static class GetExperimentRunEndpoint
{
    public static RouteGroupBuilder MapGetExperimentRun(this RouteGroupBuilder group)
    {
        group.MapGet("/experiments/{runId:guid}", async Task<Results<Ok<ExperimentRunSummary>, NotFound>> (
                Guid runId,
                IKnowledgeReader reader,
                CancellationToken cancellationToken) =>
            {
                var summary = await GetExperimentRunHandler.HandleAsync(runId, reader, cancellationToken);

                return summary is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(summary);
            })
            .WithName("GetExperimentRun");

        return group;
    }
}
