using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Domain;
using Norn.Shop.Catalog.API.Features.GetProducts;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.GetProducts;

public sealed class GetProductsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RepositoryReturnsItems_MapsToResponseWithSamePaging()
    {
        var repository = Substitute.For<IProductRepository>();
        var products = new[] { new Product(Guid.NewGuid(), "Produto", "desc", 10m, "BRL", "Categoria", 5) };
        repository.GetPagedAsync(2, 10, "Categoria", Arg.Any<CancellationToken>())
            .Returns((products, 42));

        var request = new GetProductsRequest { Page = 2, PageSize = 10, Category = "Categoria" };

        var response = await GetProductsHandler.HandleAsync(request, repository, TestContext.Current.CancellationToken);

        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(10);
        response.Total.ShouldBe(42);
        response.Items.Count.ShouldBe(1);
        response.Items[0].Name.ShouldBe("Produto");
    }
}
