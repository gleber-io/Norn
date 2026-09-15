using Norn.Analyzer.Settings;
using Norn.Analyzer.SeverityCalculation;
using Norn.Contracts;
using Shouldly;
using Xunit;

namespace Norn.Analyzer.UnitTests.SeverityCalculation;

public sealed class SeverityCalculatorTests
{
    private const string SloMetric = "http_server_request_duration_seconds_bucket";
    private const string DeviationMetric = "dotnet_process_memory_working_set_bytes";

    private static SeverityCalculator CreateCalculator() => new(new SeverityBandOptions());

    [Theory]
    [InlineData(0.10, Severity.Low)]
    [InlineData(0.20, Severity.Medium)]
    [InlineData(0.27, Severity.High)]
    [InlineData(0.35, Severity.Critical)]
    public void Calculate_SloBasedMetric_MapsDistanceToBand(double observedSeconds, Severity expected)
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(SloMetric, observedSeconds);

        result.Severity.ShouldBe(expected);
    }

    [Theory]
    [InlineData(210_000_000, Severity.Low)]
    [InlineData(260_000_000, Severity.Medium)]
    [InlineData(370_000_000, Severity.High)]
    [InlineData(520_000_000, Severity.Critical)]
    public void Calculate_DeviationBasedMetric_MapsRelativeDeviationToBand(double observedBytes, Severity expected)
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(DeviationMetric, observedBytes);

        result.Severity.ShouldBe(expected);
    }

    [Fact]
    public void Calculate_ObservedBelowBaseline_NeverElevatesSeverity()
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(DeviationMetric, observedValue: 1);

        result.Severity.ShouldBe(Severity.Low);
    }

    [Fact]
    public void Calculate_UnknownMetric_ThrowsInvalidOperation()
    {
        var calculator = CreateCalculator();

        Should.Throw<InvalidOperationException>(() => calculator.Calculate("metrica_desconhecida", 1));
    }

    [Fact]
    public void Calculate_ReturnsConfiguredReferenceValueAsExpectedValue()
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(SloMetric, 0.1);

        result.ExpectedValue.ShouldBe(0.300);
    }
}
