using Norn.Shop.Order.API.Features.CreateOrder;
using Shouldly;
using Xunit;

namespace Norn.Shop.Order.API.UnitTests.Features.CreateOrder;

public sealed class CreateOrderValidatorTests
{
    private readonly CreateOrderValidator _validator = new();

    private static CreateOrderRequest ValidRequest() => new()
    {
        CustomerId = Guid.NewGuid(),
        Currency = "BRL",
        Items = [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m }],
    };

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyItems_HasError()
    {
        var request = ValidRequest() with { Items = [] };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ItemWithNonPositiveQuantity_HasError()
    {
        var request = ValidRequest() with { Items = [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 0, UnitPrice = 10m }] };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_CurrencyWithWrongLength_HasError()
    {
        var request = ValidRequest() with { Currency = "R$" };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_EmptyCustomerId_HasError()
    {
        var request = ValidRequest() with { CustomerId = Guid.Empty };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
