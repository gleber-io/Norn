using Norn.Knowledge;
using Shouldly;
using Xunit;

namespace Norn.Knowledge.UnitTests;

/// <summary>Configuração inválida nunca derruba o loop (§5.7) — sempre volta desligada.</summary>
public sealed class ForecastConfigParserTests
{
    [Fact]
    public void ParseOrDefault_NullRawJson_ReturnsDisabledAndNotInvalid()
    {
        var config = ForecastConfigParser.ParseOrDefault(null, out var wasInvalid);

        config.Enabled.ShouldBeFalse();
        wasInvalid.ShouldBeFalse();
    }

    [Fact]
    public void ParseOrDefault_EmptyRawJson_ReturnsDisabledAndNotInvalid()
    {
        var config = ForecastConfigParser.ParseOrDefault(string.Empty, out var wasInvalid);

        config.Enabled.ShouldBeFalse();
        wasInvalid.ShouldBeFalse();
    }

    [Fact]
    public void ParseOrDefault_ValidEnabledJson_ReturnsParsedValues()
    {
        var config = ForecastConfigParser.ParseOrDefault("""{"enabled": true, "horizonMinutes": 10}""", out var wasInvalid);

        config.Enabled.ShouldBeTrue();
        config.HorizonMinutes.ShouldBe(10);
        wasInvalid.ShouldBeFalse();
    }

    [Fact]
    public void ParseOrDefault_OmittedHorizonMinutes_DefaultsToFive()
    {
        var config = ForecastConfigParser.ParseOrDefault("""{"enabled": true}""", out var wasInvalid);

        config.HorizonMinutes.ShouldBe(5);
        wasInvalid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"enabled": true, "horizonMinutes": 0}""")]
    [InlineData("""{"enabled": true, "horizonMinutes": -5}""")]
    public void ParseOrDefault_HorizonOutOfRange_ReturnsDisabledAndMarksInvalid(string rawJson)
    {
        var config = ForecastConfigParser.ParseOrDefault(rawJson, out var wasInvalid);

        config.Enabled.ShouldBeFalse();
        wasInvalid.ShouldBeTrue();
    }

    [Fact]
    public void ParseOrDefault_MalformedJson_ReturnsDisabledAndMarksInvalid()
    {
        var config = ForecastConfigParser.ParseOrDefault("not json", out var wasInvalid);

        config.Enabled.ShouldBeFalse();
        wasInvalid.ShouldBeTrue();
    }
}
