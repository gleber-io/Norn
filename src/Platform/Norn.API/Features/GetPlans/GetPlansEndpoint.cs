using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetPlans;

public static class GetPlansEndpoint
{
    public static RouteGroupBuilder MapGetPlans(this RouteGroupBuilder group)
    {
        group.MapGet("/plans", async Task<Ok<IReadOnlyList<HealingPlan>>> (
                [AsParameters] GetPlansRequest request,
                IKnowledgeReader reader,
                CancellationToken cancellationToken) =>
            {
                var response = await GetPlansHandler.HandleAsync(request, reader, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<GetPlansRequest>()
            .WithName("GetPlans");

        return group;
    }
}
