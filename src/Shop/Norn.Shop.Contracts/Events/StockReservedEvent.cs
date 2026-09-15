namespace Norn.Shop.Contracts.Events;

public sealed record StockReservedEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required IReadOnlyList<StockReservation> Reservations { get; init; }
}

public sealed record StockReservation
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }
}
