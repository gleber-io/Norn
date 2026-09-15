using FluentValidation;
using Norn.BuildingBlocks.Chaos;

namespace Norn.Shop.Order.API.Features.AdminChaos;

public sealed class AdminChaosActivateValidator : AbstractValidator<AdminChaosActivateRequest>
{
    public AdminChaosActivateValidator()
    {
        RuleFor(r => r.ScenarioId).Must(ChaosScenarioIds.All.Contains)
            .WithMessage($"ScenarioId deve ser um de: {string.Join(", ", ChaosScenarioIds.All)}.");
    }
}
