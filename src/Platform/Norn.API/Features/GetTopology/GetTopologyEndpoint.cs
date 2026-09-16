using Microsoft.AspNetCore.Http.HttpResults;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetTopology;

public static class GetTopologyEndpoint
{
    public static RouteGroupBuilder MapGetTopology(this RouteGroupBuilder group)
    {
        group.MapGet("/topology", async Task<Ok<IReadOnlyList<TopologyInfo>>> (
                IKnowledgeReader reader,
                CancellationToken cancellationToken) =>
            {
                var response = await GetTopologyHandler.HandleAsync(reader, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetTopology");

        return group;
    }
}
