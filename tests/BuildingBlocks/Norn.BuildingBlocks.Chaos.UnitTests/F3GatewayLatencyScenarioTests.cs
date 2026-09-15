using Microsoft.Extensions.Options;
using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Chaos.Scenarios;
using Shouldly;
using Xunit;

namespace Norn.BuildingBlocks.Chaos.UnitTests;

public sealed class F3GatewayLatencyScenarioTests
{
    private readonly F3GatewayLatencyScenario _scenario = new(Options.Create(new ChaosScenarioOptions { F3RampSeconds = 180 }));

    [Fact]
    public void Intensity_AtActivation_IsZero()
    {
        _scenario.Intensity(TimeSpan.Zero).ShouldBe(0);
    }

    [Fact]
    public void Intensity_AtHalfRamp_IsHalf()
    {
        _scenario.Intensity(TimeSpan.FromSeconds(90)).ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void Intensity_PastRampEnd_ClampsAtOne()
    {
        _scenario.Intensity(TimeSpan.FromSeconds(400)).ShouldBe(1.0);
    }

    [Fact]
    public void Id_And_TargetService_AreFixed()
    {
        _scenario.Id.ShouldBe("F3");
        _scenario.TargetService.ShouldBe(ChaosServiceNames.Payment);
    }
}
