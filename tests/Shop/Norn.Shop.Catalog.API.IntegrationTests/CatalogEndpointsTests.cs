using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Norn.Shop.Catalog.API.Domain;
using Norn.Shop.Catalog.API.Features;
using Norn.Shop.Catalog.API.Features.CreateProduct;
using Norn.Shop.Catalog.API.Features.GetProducts;
using Norn.Shop.Catalog.API.Infrastructure;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.IntegrationTests;

/// <summary>Cobre o que só o adaptador real prova: migration, índice e round-trip de persistência (Fase 2, tarefa 10).</summary>
public sealed class CatalogEndpointsTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetProducts_AfterMigrationAndSeed_ReturnsAtLeastFiveHundredSeededProducts()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products?page=1&pageSize=20", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetProductsResponse>(JsonOptions, TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(20);
        body.Total.ShouldBeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task CreateThenGetProduct_RoundTripsThroughRealPostgres()
    {
        var client = factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = $"Produto Integração {Guid.NewGuid()}",
            Description = "Criado pelo teste de integração",
            Price = 42.50m,
            Currency = "BRL",
            Category = "Testes",
            Stock = 7,
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/products", request, JsonOptions, TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions, TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        var getResponse = await client.GetAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.Name.ShouldBe(request.Name);
        fetched.Stock.ShouldBe(request.Stock);
        fetched.Price.ShouldBe(request.Price);
    }

    [Fact]
    public async Task GetProductById_UnknownId_ReturnsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Migration_CreatesUniqueIndexOnProductName_RealPostgresRejectsDuplicateInsert()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var name = $"Duplicado {Guid.NewGuid()}";

        dbContext.Products.Add(new Product(Guid.NewGuid(), name, "d", 1m, "BRL", "Cat", 1));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Products.Add(new Product(Guid.NewGuid(), name, "d", 1m, "BRL", "Cat", 1));

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
