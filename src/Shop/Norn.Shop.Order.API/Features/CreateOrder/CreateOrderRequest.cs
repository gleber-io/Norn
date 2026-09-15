namespace Norn.Shop.Order.API.Features.CreateOrder;

/// <summary>
/// A §5.2 resume o corpo como <c>{customerId, items[]{productId,quantity}}</c>; <c>currency</c> e
/// <c>unitPrice</c> por item entram aqui porque <c>OrderCreatedEvent</c> (§5.1) os exige e o
/// Order.API não conhece preço de produto — o carrinho os traz de uma leitura prévia de
/// <c>GET /api/v1/products</c>, sem acoplar Order a uma chamada síncrona ao Catalog.
/// </summary>
public sealed record CreateOrderRequest
{
    public required Guid CustomerId { get; init; }

    public required string Currency { get; init; }

    public required IReadOnlyList<CreateOrderItem> Items { get; init; }
}

public sealed record CreateOrderItem
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }

    public required decimal UnitPrice { get; init; }
}
