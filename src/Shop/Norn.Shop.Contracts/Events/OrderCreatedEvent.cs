namespace Norn.Shop.Contracts.Events;

public sealed record OrderCreatedEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid CustomerId { get; init; }

    public required IReadOnlyList<OrderLineItem> Items { get; init; }

    public required decimal TotalAmount { get; init; }

    public required string Currency { get; init; }
}

public sealed record OrderLineItem
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }

    public required decimal UnitPrice { get; init; }
}
