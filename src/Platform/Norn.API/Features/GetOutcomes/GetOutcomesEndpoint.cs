using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetOutcomes;

public static class GetOutcomesEndpoint
{
    public static RouteGroupBuilder MapGetOutcomes(this RouteGroupBuilder group)
    {
        group.MapGet("/outcomes", async Task<Ok<IReadOnlyList<HealingOutcome>>> (
                [AsParameters] GetOutcomesRequest request,
                IKnowledgeReader reader,
                CancellationToken cancellationToken) =>
            {
                var response = await GetOutcomesHandler.HandleAsync(request, reader, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<GetOutcomesRequest>()
            .WithName("GetOutcomes");

        return group;
    }
}
