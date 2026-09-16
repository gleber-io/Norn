using Microsoft.AspNetCore.Http.HttpResults;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetMode;

public static class GetModeEndpoint
{
    public static RouteGroupBuilder MapGetMode(this RouteGroupBuilder group)
    {
        group.MapGet("/mode", async Task<Ok<ModeResponse>> (
                IPlatformConfig platformConfig,
                CancellationToken cancellationToken) =>
            {
                var response = await GetModeHandler.HandleAsync(platformConfig, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetMode");

        return group;
    }
}
