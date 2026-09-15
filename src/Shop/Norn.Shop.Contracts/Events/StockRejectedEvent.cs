namespace Norn.Shop.Contracts.Events;

public sealed record StockRejectedEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid ProductId { get; init; }

    public required int Available { get; init; }

    public required int Requested { get; init; }
}
