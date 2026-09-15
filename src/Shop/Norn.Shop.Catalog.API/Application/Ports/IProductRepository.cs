using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Application.Ports;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Product> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? category,
        CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
