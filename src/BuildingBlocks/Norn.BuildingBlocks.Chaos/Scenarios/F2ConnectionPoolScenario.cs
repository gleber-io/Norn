using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Scenarios;

/// <summary>F2 — estrangulamento de concorrência em Order, rampa linear até saturar em 1,0.</summary>
public sealed class F2ConnectionPoolScenario(IOptions<ChaosScenarioOptions> options) : IChaosScenario
{
    public string Id => ChaosScenarioIds.F2;

    public string TargetService => ChaosServiceNames.Order;

    public double Intensity(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var ramp = options.Value.F2RampSeconds;
        return Math.Clamp(elapsed.TotalSeconds / ramp, 0, 1);
    }
}
