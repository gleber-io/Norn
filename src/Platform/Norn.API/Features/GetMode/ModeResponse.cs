using Norn.Contracts.Ports;

namespace Norn.API.Features.GetMode;

public sealed record ModeResponse
{
    public required PlatformMode Mode { get; init; }
}
