using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Features;

public sealed record OrderResponse
{
    public required Guid Id { get; init; }

    public required Guid CustomerId { get; init; }

    public required IReadOnlyList<OrderItemResponse> Items { get; init; }

    public required decimal TotalAmount { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }

    public string? RejectionReason { get; init; }

    public static OrderResponse FromDomain(CustomerOrder order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Items = order.Items.Select(OrderItemResponse.FromDomain).ToList(),
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        Status = order.Status.ToString(),
        RejectionReason = order.RejectionReason,
    };
}

public sealed record OrderItemResponse
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }

    public required decimal UnitPrice { get; init; }

    public static OrderItemResponse FromDomain(OrderItem item) => new()
    {
        ProductId = item.ProductId,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
    };
}
