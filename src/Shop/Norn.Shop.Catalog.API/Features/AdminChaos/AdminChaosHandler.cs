using Norn.BuildingBlocks.Chaos;

namespace Norn.Shop.Catalog.API.Features.AdminChaos;

public static class AdminChaosHandler
{
    public static Task ActivateAsync(AdminChaosActivateRequest request, IChaosActivationStore store, CancellationToken cancellationToken) =>
        store.ActivateAsync(request.ScenarioId, request.Seed, DateTimeOffset.UtcNow, cancellationToken);

    public static Task DeactivateAsync(IChaosActivationStore store, CancellationToken cancellationToken) =>
        store.DeactivateAsync(cancellationToken);
}
