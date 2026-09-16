using FluentValidation;

namespace Norn.API.Features.GetOutcomes;

public sealed class GetOutcomesValidator : AbstractValidator<GetOutcomesRequest>
{
    public GetOutcomesValidator()
    {
        RuleFor(r => r.Limit).InclusiveBetween(1, 500);
    }
}
