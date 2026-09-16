using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetSignals;

public static class GetSignalsEndpoint
{
    public static RouteGroupBuilder MapGetSignals(this RouteGroupBuilder group)
    {
        group.MapGet("/signals", async Task<Ok<IReadOnlyList<AnomalySignal>>> (
                [AsParameters] GetSignalsRequest request,
                IKnowledgeReader reader,
                CancellationToken cancellationToken) =>
            {
                var response = await GetSignalsHandler.HandleAsync(request, reader, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<GetSignalsRequest>()
            .WithName("GetSignals");

        return group;
    }
}
