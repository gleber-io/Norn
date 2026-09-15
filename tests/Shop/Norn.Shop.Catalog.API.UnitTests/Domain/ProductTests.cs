using Norn.Shop.Catalog.API.Domain;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void TryReserveStock_QuantityWithinAvailable_ReservesAndReturnsTrue()
    {
        var product = new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", stock: 5);

        var result = product.TryReserveStock(3);

        result.ShouldBeTrue();
        product.Stock.ShouldBe(2);
    }

    [Fact]
    public void TryReserveStock_QuantityAboveAvailable_ReturnsFalseAndLeavesStockUnchanged()
    {
        var product = new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", stock: 5);

        var result = product.TryReserveStock(6);

        result.ShouldBeFalse();
        product.Stock.ShouldBe(5);
    }

    [Fact]
    public void TryReserveStock_ExactAvailable_ReservesDownToZero()
    {
        var product = new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", stock: 5);

        var result = product.TryReserveStock(5);

        result.ShouldBeTrue();
        product.Stock.ShouldBe(0);
    }

    [Fact]
    public void TryReserveStock_NonPositiveQuantity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", stock: 5).TryReserveStock(0));

    [Fact]
    public void Constructor_EmptyName_Throws() =>
        Should.Throw<ArgumentException>(() => new Product(Guid.NewGuid(), string.Empty, "desc", 10m, "BRL", "Categoria", stock: 5));

    [Fact]
    public void Constructor_NegativePrice_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Produto", "desc", -1m, "BRL", "Categoria", stock: 5));

    [Fact]
    public void Constructor_NegativeStock_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", stock: -1));
}
