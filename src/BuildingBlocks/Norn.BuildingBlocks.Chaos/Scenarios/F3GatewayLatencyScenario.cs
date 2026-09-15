using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Scenarios;

/// <summary>F3 — latência crescente no gateway simulado de Payment, rampa linear até saturar em 1,0.</summary>
public sealed class F3GatewayLatencyScenario(IOptions<ChaosScenarioOptions> options) : IChaosScenario
{
    public string Id => ChaosScenarioIds.F3;

    public string TargetService => ChaosServiceNames.Payment;

    public double Intensity(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var ramp = options.Value.F3RampSeconds;
        return Math.Clamp(elapsed.TotalSeconds / ramp, 0, 1);
    }
}
