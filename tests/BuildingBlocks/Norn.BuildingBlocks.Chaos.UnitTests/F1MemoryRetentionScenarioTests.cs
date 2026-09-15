using Microsoft.Extensions.Options;
using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Chaos.Scenarios;
using Shouldly;
using Xunit;

namespace Norn.BuildingBlocks.Chaos.UnitTests;

public sealed class F1MemoryRetentionScenarioTests
{
    private readonly F1MemoryRetentionScenario _scenario = new(Options.Create(new ChaosScenarioOptions { F1RampSeconds = 90 }));

    [Fact]
    public void Intensity_AtActivation_IsZero()
    {
        _scenario.Intensity(TimeSpan.Zero).ShouldBe(0);
    }

    [Fact]
    public void Intensity_AtOneTau_MatchesClosedForm()
    {
        var expected = 1 - Math.Exp(-1);
        _scenario.Intensity(TimeSpan.FromSeconds(90)).ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void Intensity_AfterManyTau_SaturatesNearOne()
    {
        _scenario.Intensity(TimeSpan.FromHours(1)).ShouldBeGreaterThan(0.999);
    }

    [Fact]
    public void Intensity_IsMonotonicallyNonDecreasing()
    {
        var earlier = _scenario.Intensity(TimeSpan.FromSeconds(30));
        var later = _scenario.Intensity(TimeSpan.FromSeconds(60));

        later.ShouldBeGreaterThan(earlier);
    }

    [Fact]
    public void Id_And_TargetService_AreFixed()
    {
        _scenario.Id.ShouldBe("F1");
        _scenario.TargetService.ShouldBe(ChaosServiceNames.Catalog);
    }
}
