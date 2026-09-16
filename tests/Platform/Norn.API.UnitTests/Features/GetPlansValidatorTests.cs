using Norn.API.Features.GetPlans;
using Shouldly;
using Xunit;

namespace Norn.API.UnitTests.Features;

public sealed class GetPlansValidatorTests
{
    private readonly GetPlansValidator validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void Validate_LimitWithinRange_IsValid(int limit)
    {
        var result = validator.Validate(new GetPlansRequest { Limit = limit });

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Validate_LimitOutOfRange_IsInvalid(int limit)
    {
        var result = validator.Validate(new GetPlansRequest { Limit = limit });

        result.IsValid.ShouldBeFalse();
    }
}
