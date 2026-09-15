using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Features.GetProductById;

public static class GetProductByIdHandler
{
    public static Task<Product?> HandleAsync(Guid id, IProductRepository repository, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(id, cancellationToken);
}
