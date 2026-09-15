using FluentValidation;

namespace Norn.Shop.Catalog.API.Features.GetProducts;

public sealed class GetProductsValidator : AbstractValidator<GetProductsRequest>
{
    public GetProductsValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100);
    }
}
