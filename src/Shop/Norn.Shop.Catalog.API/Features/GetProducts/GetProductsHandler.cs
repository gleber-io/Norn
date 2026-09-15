using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Features.GetProducts;

public static class GetProductsHandler
{
    public static async Task<GetProductsResponse> HandleAsync(
        GetProductsRequest request,
        IProductRepository repository,
        CancellationToken cancellationToken)
    {
        var (items, total) = await repository.GetPagedAsync(request.Page, request.PageSize, request.Category, cancellationToken);

        return new GetProductsResponse
        {
            Items = items.Select(ProductResponse.FromDomain).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            Total = total,
        };
    }
}
