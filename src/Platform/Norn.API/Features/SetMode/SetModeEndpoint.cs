using Microsoft.AspNetCore.Http.HttpResults;
using Norn.API.Features.GetMode;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Contracts.Ports;

namespace Norn.API.Features.SetMode;

public static class SetModeEndpoint
{
    public static RouteGroupBuilder MapSetMode(this RouteGroupBuilder group)
    {
        group.MapPut("/mode", async Task<Ok<ModeResponse>> (
                SetModeRequest request,
                IPlatformConfig platformConfig,
                CancellationToken cancellationToken) =>
            {
                var response = await SetModeHandler.HandleAsync(request, platformConfig, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<SetModeRequest>()
            .WithName("SetMode");

        return group;
    }
}
