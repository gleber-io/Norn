using FluentValidation;

namespace Norn.Shop.Catalog.API.Features.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Price).GreaterThan(0);
        RuleFor(r => r.Currency).NotEmpty().Length(3);
        RuleFor(r => r.Category).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Stock).GreaterThanOrEqualTo(0);
    }
}
