using Norn.Contracts.Ports;

namespace Norn.API.Features.SetMode;

public sealed record SetModeRequest
{
    public required PlatformMode Mode { get; init; }
}
