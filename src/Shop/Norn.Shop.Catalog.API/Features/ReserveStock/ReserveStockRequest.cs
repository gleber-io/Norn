namespace Norn.Shop.Catalog.API.Features.ReserveStock;

public sealed record ReserveStockRequest
{
    public required Guid OrderId { get; init; }

    public required IReadOnlyList<ReserveStockItem> Items { get; init; }
}

public sealed record ReserveStockItem
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }
}

public sealed record StockReservation(Guid ProductId, int Quantity);

public sealed record StockRejection(Guid ProductId, int Available, int Requested);

public sealed record ReserveStockResult(IReadOnlyList<StockReservation> Reserved, IReadOnlyList<StockRejection> Rejections);
