using Norn.Contracts;
using Norn.Planner.Settings;
using Shouldly;
using Xunit;

namespace Norn.Planner.UnitTests.Settings;

/// <summary>
/// <see cref="PlannerOptions.VerificationWindowSecondsFor"/> — residual de calibração da Fase 9
/// (o achado do replay ao vivo: 120s genéricos não bastam para um pod .NET recém-criado por
/// <c>RestartPod</c> assentar o RSS, mesmo sem vazamento nenhum rolando).
/// </summary>
public sealed class PlannerOptionsTests
{
    [Fact]
    public void VerificationWindowSecondsFor_RestartPod_ReturnsDedicatedWindow()
    {
        var options = new PlannerOptions { VerificationWindowSeconds = 120, RestartPodVerificationWindowSeconds = 240 };

        options.VerificationWindowSecondsFor(HealingActionType.RestartPod).ShouldBe(240);
    }

    [Theory]
    [InlineData(HealingActionType.ScaleUp)]
    [InlineData(HealingActionType.ToggleFeatureFlag)]
    [InlineData(HealingActionType.NoOp)]
    public void VerificationWindowSecondsFor_OtherActionTypes_ReturnsGenericWindow(HealingActionType actionType)
    {
        var options = new PlannerOptions { VerificationWindowSeconds = 120, RestartPodVerificationWindowSeconds = 240 };

        options.VerificationWindowSecondsFor(actionType).ShouldBe(120);
    }
}
