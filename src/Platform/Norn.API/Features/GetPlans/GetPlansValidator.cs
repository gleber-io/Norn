using FluentValidation;

namespace Norn.API.Features.GetPlans;

public sealed class GetPlansValidator : AbstractValidator<GetPlansRequest>
{
    public GetPlansValidator()
    {
        RuleFor(r => r.Limit).InclusiveBetween(1, 500);
    }
}
