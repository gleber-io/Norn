using FluentValidation;

namespace Norn.API.Features.SetMode;

public sealed class SetModeValidator : AbstractValidator<SetModeRequest>
{
    public SetModeValidator()
    {
        RuleFor(r => r.Mode).IsInEnum();
    }
}
