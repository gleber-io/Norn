using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Features.CreateProduct;

public static class CreateProductEndpoint
{
    public static RouteGroupBuilder MapCreateProduct(this RouteGroupBuilder group)
    {
        group.MapPost("/products", async Task<Results<Created<ProductResponse>, Conflict>> (
                CreateProductRequest request,
                IProductRepository repository,
                ICatalogMetrics metrics,
                CancellationToken cancellationToken) =>
            {
                var product = await CreateProductHandler.HandleAsync(request, repository, metrics, cancellationToken);

                return product is null
                    ? TypedResults.Conflict()
                    : TypedResults.Created($"/api/v1/products/{product.Id}", ProductResponse.FromDomain(product));
            })
            .WithValidation<CreateProductRequest>()
            .WithName("CreateProduct");

        return group;
    }
}
