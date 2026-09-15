using FluentValidation;

namespace Norn.Shop.Payment.API.Features.ProcessPayment;

public sealed class ProcessPaymentValidator : AbstractValidator<ProcessPaymentRequest>
{
    public ProcessPaymentValidator()
    {
        RuleFor(r => r.OrderId).NotEmpty();
        RuleFor(r => r.Amount).GreaterThan(0);
        RuleFor(r => r.Currency).NotEmpty().Length(3);
        RuleFor(r => r.Method).NotEmpty().MaximumLength(50);
    }
}
