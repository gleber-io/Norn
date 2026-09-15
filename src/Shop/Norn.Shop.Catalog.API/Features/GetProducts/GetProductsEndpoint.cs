using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Features.GetProducts;

public static class GetProductsEndpoint
{
    public static RouteGroupBuilder MapGetProducts(this RouteGroupBuilder group)
    {
        group.MapGet("/products", async Task<Ok<GetProductsResponse>> (
                [AsParameters] GetProductsRequest request,
                IProductRepository repository,
                CancellationToken cancellationToken) =>
            {
                var response = await GetProductsHandler.HandleAsync(request, repository, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithValidation<GetProductsRequest>()
            .WithName("GetProducts");

        return group;
    }
}
