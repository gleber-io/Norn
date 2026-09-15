namespace Norn.Shop.Catalog.API.Features.GetProducts;

public sealed record GetProductsRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Category { get; init; }
}

public sealed record GetProductsResponse
{
    public required IReadOnlyList<ProductResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int Total { get; init; }
}
