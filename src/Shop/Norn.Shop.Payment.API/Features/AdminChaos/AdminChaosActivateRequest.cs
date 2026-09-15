namespace Norn.Shop.Payment.API.Features.AdminChaos;

public sealed class AdminChaosActivateRequest
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }
}
