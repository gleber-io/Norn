using FluentValidation;

namespace Norn.Shop.Catalog.API.Features.ReserveStock;

public sealed class ReserveStockValidator : AbstractValidator<ReserveStockRequest>
{
    public ReserveStockValidator()
    {
        RuleFor(r => r.Items).NotEmpty();
        RuleForEach(r => r.Items).ChildRules(item => item.RuleFor(i => i.Quantity).GreaterThan(0));
    }
}
