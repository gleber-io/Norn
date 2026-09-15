using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Features.CreateProduct;

public static class CreateProductHandler
{
    /// <summary>Retorna null quando já existe produto com o mesmo nome (409 na borda).</summary>
    public static async Task<Product?> HandleAsync(
        CreateProductRequest request,
        IProductRepository repository,
        ICatalogMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsByNameAsync(request.Name, cancellationToken))
        {
            return null;
        }

        var product = new Product(
            Guid.NewGuid(),
            request.Name,
            request.Description ?? string.Empty,
            request.Price,
            request.Currency,
            request.Category,
            request.Stock);

        await repository.AddAsync(product, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        metrics.RecordProductCreated();

        return product;
    }
}
