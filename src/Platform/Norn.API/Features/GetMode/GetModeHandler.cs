using Norn.Contracts.Ports;

namespace Norn.API.Features.GetMode;

public static class GetModeHandler
{
    public static async Task<ModeResponse> HandleAsync(IPlatformConfig platformConfig, CancellationToken cancellationToken) =>
        new() { Mode = await platformConfig.GetModeAsync(cancellationToken) };
}
