using Norn.Shop.Contracts.Events;

namespace Norn.BuildingBlocks.Messaging;

/// <summary>Convenções de exchange/routing key da §5.1 — fonte única, sem cópia por serviço.</summary>
public static class NornMessagingTopology
{
    private static readonly Dictionary<Type, (string Exchange, string RoutingKey)> Conventions =
        new()
        {
            [typeof(OrderCreatedEvent)] = ("shop.orders", "order.created"),
            [typeof(PaymentApprovedEvent)] = ("shop.payments", "payment.approved"),
            [typeof(PaymentDeclinedEvent)] = ("shop.payments", "payment.declined"),
            [typeof(StockReservedEvent)] = ("shop.catalog", "stock.reserved"),
            [typeof(StockRejectedEvent)] = ("shop.catalog", "stock.rejected"),
        };

    public static string GetExchange<TEvent>() => Conventions[typeof(TEvent)].Exchange;

    public static string GetRoutingKey<TEvent>() => Conventions[typeof(TEvent)].RoutingKey;
}
