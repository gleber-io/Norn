using Norn.Labeler.Detection;
using Shouldly;
using Xunit;

namespace Norn.Labeler.UnitTests;

public sealed class LoadDeliveryCheckerTests
{
    [Theory]
    [InlineData(10.0, 10.0, true)]
    [InlineData(10.0, 9.0, true)]
    [InlineData(10.0, 11.0, true)]
    [InlineData(10.0, 8.99, false)]
    [InlineData(10.0, 11.01, false)]
    [InlineData(0.0, 5.0, false)]
    public void IsWithinTarget_EvaluatesTenPercentBand(double targetRps, double achievedRps, bool expected)
    {
        LoadDeliveryChecker.IsWithinTarget(targetRps, achievedRps).ShouldBe(expected);
    }
}
