using Norn.Shop.Catalog.API.Features.ReserveStock;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.ReserveStock;

public sealed class ReserveStockValidatorTests
{
    private readonly ReserveStockValidator _validator = new();

    [Fact]
    public void Validate_AtLeastOneItemWithPositiveQuantity_IsValid()
    {
        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items = [new ReserveStockItem { ProductId = Guid.NewGuid(), Quantity = 1 }],
        };

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NoItems_IsInvalid()
    {
        var request = new ReserveStockRequest { OrderId = Guid.NewGuid(), Items = [] };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ItemWithZeroQuantity_IsInvalid()
    {
        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items = [new ReserveStockItem { ProductId = Guid.NewGuid(), Quantity = 0 }],
        };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }
}
