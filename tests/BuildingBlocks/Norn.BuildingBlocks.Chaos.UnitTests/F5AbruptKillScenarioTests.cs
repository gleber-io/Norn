using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Chaos.Scenarios;
using Shouldly;
using Xunit;

namespace Norn.BuildingBlocks.Chaos.UnitTests;

public sealed class F5AbruptKillScenarioTests
{
    private readonly F5AbruptKillScenario _scenario = new();

    [Fact]
    public void Intensity_AtActivation_IsAlreadyMaximum()
    {
        _scenario.Intensity(TimeSpan.Zero).ShouldBe(1.0);
    }

    [Fact]
    public void Intensity_AnyTimeAfterActivation_StaysAtMaximum()
    {
        _scenario.Intensity(TimeSpan.FromMinutes(10)).ShouldBe(1.0);
    }

    [Fact]
    public void Id_And_TargetService_AreFixed()
    {
        _scenario.Id.ShouldBe("F5");
        _scenario.TargetService.ShouldBe(ChaosServiceNames.Catalog);
    }
}
