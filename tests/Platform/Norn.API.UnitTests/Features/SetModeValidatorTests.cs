using Norn.API.Features.SetMode;
using Norn.Contracts.Ports;
using Shouldly;
using Xunit;

namespace Norn.API.UnitTests.Features;

public sealed class SetModeValidatorTests
{
    private readonly SetModeValidator validator = new();

    [Theory]
    [InlineData(PlatformMode.Observe)]
    [InlineData(PlatformMode.DryRun)]
    [InlineData(PlatformMode.Active)]
    public void Validate_DefinedMode_IsValid(PlatformMode mode)
    {
        var result = validator.Validate(new SetModeRequest { Mode = mode });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_UndefinedMode_IsInvalid()
    {
        var result = validator.Validate(new SetModeRequest { Mode = (PlatformMode)99 });

        result.IsValid.ShouldBeFalse();
    }
}
