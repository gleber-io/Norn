namespace Norn.Shop.Catalog.API.Features.CreateProduct;

public sealed record CreateProductRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required decimal Price { get; init; }

    public required string Currency { get; init; }

    public required string Category { get; init; }

    public required int Stock { get; init; }
}
