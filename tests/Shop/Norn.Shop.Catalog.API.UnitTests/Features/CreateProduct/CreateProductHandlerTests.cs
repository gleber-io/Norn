using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Domain;
using Norn.Shop.Catalog.API.Features.CreateProduct;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.CreateProduct;

public sealed class CreateProductHandlerTests
{
    private static readonly CreateProductRequest Request = new()
    {
        Name = "Produto Novo",
        Description = "desc",
        Price = 19.90m,
        Currency = "BRL",
        Category = "Categoria",
        Stock = 10,
    };

    [Fact]
    public async Task HandleAsync_NameNotTaken_PersistsAndReturnsProduct()
    {
        var repository = Substitute.For<IProductRepository>();
        repository.ExistsByNameAsync(Request.Name, Arg.Any<CancellationToken>()).Returns(false);
        var metrics = Substitute.For<ICatalogMetrics>();

        var product = await CreateProductHandler.HandleAsync(Request, repository, metrics, TestContext.Current.CancellationToken);

        product.ShouldNotBeNull();
        product.Name.ShouldBe(Request.Name);
        await repository.Received(1).AddAsync(Arg.Is<Product>(p => p.Name == Request.Name), Arg.Any<CancellationToken>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NameAlreadyExists_ReturnsNullWithoutPersisting()
    {
        var repository = Substitute.For<IProductRepository>();
        repository.ExistsByNameAsync(Request.Name, Arg.Any<CancellationToken>()).Returns(true);
        var metrics = Substitute.For<ICatalogMetrics>();

        var product = await CreateProductHandler.HandleAsync(Request, repository, metrics, TestContext.Current.CancellationToken);

        product.ShouldBeNull();
        await repository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }
}
