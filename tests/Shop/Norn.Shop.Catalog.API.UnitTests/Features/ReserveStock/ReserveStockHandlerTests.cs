using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Domain;
using Norn.Shop.Catalog.API.Features.ReserveStock;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.ReserveStock;

public sealed class ReserveStockHandlerTests
{
    [Fact]
    public async Task HandleAsync_StockCoversAllItems_ReservesEverythingWithNoRejections()
    {
        var productId = Guid.NewGuid();
        var product = new Product(productId, "Produto", "desc", 10m, "BRL", "Categoria", stock: 10);
        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdAsync(productId, Arg.Any<CancellationToken>()).Returns(product);
        var metrics = Substitute.For<ICatalogMetrics>();

        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items = [new ReserveStockItem { ProductId = productId, Quantity = 4 }],
        };

        var result = await ReserveStockHandler.HandleAsync(request, repository, metrics, TestContext.Current.CancellationToken);

        result.Reserved.ShouldHaveSingleItem();
        result.Rejections.ShouldBeEmpty();
        product.Stock.ShouldBe(6);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_StockBelowRequested_RejectsWithoutMutatingStock()
    {
        var productId = Guid.NewGuid();
        var product = new Product(productId, "Produto", "desc", 10m, "BRL", "Categoria", stock: 2);
        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdAsync(productId, Arg.Any<CancellationToken>()).Returns(product);
        var metrics = Substitute.For<ICatalogMetrics>();

        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items = [new ReserveStockItem { ProductId = productId, Quantity = 5 }],
        };

        var result = await ReserveStockHandler.HandleAsync(request, repository, metrics, TestContext.Current.CancellationToken);

        result.Reserved.ShouldBeEmpty();
        result.Rejections.ShouldHaveSingleItem();
        result.Rejections[0].Available.ShouldBe(2);
        result.Rejections[0].Requested.ShouldBe(5);
        product.Stock.ShouldBe(2);
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MixOfSufficientAndInsufficientItems_PartitionsReservedAndRejected()
    {
        var coveredId = Guid.NewGuid();
        var shortId = Guid.NewGuid();
        var covered = new Product(coveredId, "Coberto", "desc", 10m, "BRL", "Categoria", stock: 10);
        var shortOnStock = new Product(shortId, "Curto", "desc", 10m, "BRL", "Categoria", stock: 1);

        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdAsync(coveredId, Arg.Any<CancellationToken>()).Returns(covered);
        repository.GetByIdAsync(shortId, Arg.Any<CancellationToken>()).Returns(shortOnStock);
        var metrics = Substitute.For<ICatalogMetrics>();

        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items =
            [
                new ReserveStockItem { ProductId = coveredId, Quantity = 3 },
                new ReserveStockItem { ProductId = shortId, Quantity = 3 },
            ],
        };

        var result = await ReserveStockHandler.HandleAsync(request, repository, metrics, TestContext.Current.CancellationToken);

        result.Reserved.ShouldHaveSingleItem();
        result.Reserved[0].ProductId.ShouldBe(coveredId);
        result.Rejections.ShouldHaveSingleItem();
        result.Rejections[0].ProductId.ShouldBe(shortId);
    }

    [Fact]
    public async Task HandleAsync_ProductDoesNotExist_RejectsWithZeroAvailable()
    {
        var missingId = Guid.NewGuid();
        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Product?)null);
        var metrics = Substitute.For<ICatalogMetrics>();

        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items = [new ReserveStockItem { ProductId = missingId, Quantity = 1 }],
        };

        var result = await ReserveStockHandler.HandleAsync(request, repository, metrics, TestContext.Current.CancellationToken);

        result.Rejections.ShouldHaveSingleItem();
        result.Rejections[0].Available.ShouldBe(0);
    }
}
