namespace Norn.Shop.Order.API.Features.CreateOrder;

public sealed record CreateOrderResponse
{
    public required Guid OrderId { get; init; }

    public required string Status { get; init; }
}
