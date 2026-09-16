using Norn.API.Features.GetMode;
using Norn.Contracts.Ports;

namespace Norn.API.Features.SetMode;

/// <summary>
/// Chama só <see cref="IPlatformConfig.SetModeAsync"/> — nenhuma publicação em
/// <c>norn:events</c> aqui. O único publicador do canal é Norn.Worker (ADR-15); a mudança de
/// modo chega ao dashboard via o diff de <c>PumpAsync</c> no próximo ciclo de polling (§0.2 do
/// plano da Fase 10), não por este handler.
/// </summary>
public static class SetModeHandler
{
    public static async Task<ModeResponse> HandleAsync(SetModeRequest request, IPlatformConfig platformConfig, CancellationToken cancellationToken)
    {
        await platformConfig.SetModeAsync(request.Mode, cancellationToken);
        return new ModeResponse { Mode = request.Mode };
    }
}
