using Microsoft.AspNetCore.Http.HttpResults;
using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Features.GetProductById;

public static class GetProductByIdEndpoint
{
    public static RouteGroupBuilder MapGetProductById(this RouteGroupBuilder group)
    {
        group.MapGet("/products/{id:guid}", async Task<Results<Ok<ProductResponse>, NotFound>> (
                Guid id,
                IProductRepository repository,
                CancellationToken cancellationToken) =>
            {
                var product = await GetProductByIdHandler.HandleAsync(id, repository, cancellationToken);

                return product is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(ProductResponse.FromDomain(product));
            })
            .WithName("GetProductById");

        return group;
    }
}
