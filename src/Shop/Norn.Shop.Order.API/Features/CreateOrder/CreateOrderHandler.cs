using MassTransit;
using Norn.BuildingBlocks.Messaging;
using Norn.BuildingBlocks.Telemetry;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Application.Ports;
using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Features.CreateOrder;

public static class CreateOrderHandler
{
    /// <summary>
    /// Cria o pedido e publica <c>OrderCreated</c> antes do <c>SaveChangesAsync</c> — com o bus
    /// outbox do MassTransit (Fase 3, tarefa 4) ligado no <c>OrderDbContext</c>, a publicação só
    /// sai do outbox após o commit do agregado (§5.1: "publicação só após commit do agregado").
    /// </summary>
    public static async Task<CustomerOrder> HandleAsync(
        CreateOrderRequest request,
        IOrderRepository repository,
        IPublishEndpoint publishEndpoint,
        IOrderMetrics metrics,
        CancellationToken cancellationToken)
    {
        var items = request.Items
            .Select(i => new OrderItem(i.ProductId, i.Quantity, i.UnitPrice))
            .ToList();

        var order = new CustomerOrder(Guid.NewGuid(), request.CustomerId, request.Currency, items);

        await repository.AddAsync(order, cancellationToken);

        var @event = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(OrderCreatedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            ExperimentRunId = NornBaggage.GetExperimentRunId(),
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items
                .Select(i => new OrderLineItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice })
                .ToList(),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        };

        await publishEndpoint.PublishIntegrationEventAsync(@event, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        metrics.RecordOrderCreated();

        return order;
    }
}
