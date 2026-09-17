using Norn.API.Features.GetMetricsSeries;
using Shouldly;
using Xunit;

namespace Norn.API.UnitTests.Features;

public sealed class GetMetricsSeriesValidatorTests
{
    private readonly GetMetricsSeriesValidator validator = new();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static GetMetricsSeriesRequest ValidRequest() => new()
    {
        MetricName = "dotnet_process_memory_working_set_bytes",
        Service = "Norn.Shop.Catalog.API",
        FromUtc = Now.AddMinutes(-15),
        ToUtc = Now,
    };

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = validator.Validate(ValidRequest());

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("dotnet_process_memory_working_set_bytes; DROP TABLE x")]
    [InlineData("")]
    [InlineData("1_invalid_leading_digit")]
    public void Validate_MetricNameNotPromQlIdentifier_IsInvalid(string metricName)
    {
        var result = validator.Validate(ValidRequest() with { MetricName = metricName });

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Norn.Shop.Catalog.API\"}  or 1=1 {\"")]
    [InlineData("")]
    public void Validate_ServiceNotSafeLabelValue_IsInvalid(string service)
    {
        var result = validator.Validate(ValidRequest() with { Service = service });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ToUtcNotAfterFromUtc_IsInvalid()
    {
        var result = validator.Validate(ValidRequest() with { FromUtc = Now, ToUtc = Now });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WindowLargerThanOneHour_IsInvalid()
    {
        var result = validator.Validate(ValidRequest() with { FromUtc = Now.AddHours(-2), ToUtc = Now });

        result.IsValid.ShouldBeFalse();
    }
}
