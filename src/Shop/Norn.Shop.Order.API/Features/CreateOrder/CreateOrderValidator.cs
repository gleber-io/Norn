using FluentValidation;

namespace Norn.Shop.Order.API.Features.CreateOrder;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(r => r.CustomerId).NotEmpty();
        RuleFor(r => r.Currency).NotEmpty().Length(3);
        RuleFor(r => r.Items).NotEmpty();
        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
    }
}
