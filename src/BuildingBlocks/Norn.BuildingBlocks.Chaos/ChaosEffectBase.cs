using Microsoft.AspNetCore.Http;

namespace Norn.BuildingBlocks.Chaos;

internal abstract class ChaosEffectBase : IChaosEffect
{
    public abstract string ScenarioId { get; }

    public abstract ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken);

    public virtual ValueTask OnRequestAsync(HttpContext context, RequestDelegate next) => new(next(context));

    public virtual void Reset()
    {
    }
}
