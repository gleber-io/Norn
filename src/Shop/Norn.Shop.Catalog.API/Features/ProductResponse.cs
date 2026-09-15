using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Features;

public sealed record ProductResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required decimal Price { get; init; }

    public required string Currency { get; init; }

    public required string Category { get; init; }

    public required int Stock { get; init; }

    public static ProductResponse FromDomain(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Currency = product.Currency,
        Category = product.Category,
        Stock = product.Stock,
    };
}
