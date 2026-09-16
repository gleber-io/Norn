using FluentValidation;

namespace Norn.API.Features.GetSignals;

public sealed class GetSignalsValidator : AbstractValidator<GetSignalsRequest>
{
    public GetSignalsValidator()
    {
        RuleFor(r => r.Limit).InclusiveBetween(1, 500);
    }
}
