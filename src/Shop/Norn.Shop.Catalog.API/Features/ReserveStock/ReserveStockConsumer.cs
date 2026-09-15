using FluentValidation;
using MassTransit;
using Norn.BuildingBlocks.Messaging;
using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Contracts.Events;

namespace Norn.Shop.Catalog.API.Features.ReserveStock;

/// <summary>
/// Fronteira de entrada da feature — o equivalente de "Endpoint" para uma vertical slice
/// disparada por evento em vez de HTTP (Fase 2, tarefa 2, consumidor de <c>OrderCreated</c>).
/// </summary>
public sealed class ReserveStockConsumer(IProductRepository repository, IValidator<ReserveStockRequest> validator, ICatalogMetrics metrics)
    : IConsumer<OrderCreatedEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var order = context.Message;

        var request = new ReserveStockRequest
        {
            OrderId = order.OrderId,
            Items = order.Items.Select(i => new ReserveStockItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
        };

        await validator.ValidateAndThrowAsync(request, context.CancellationToken);

        var result = await ReserveStockHandler.HandleAsync(request, repository, metrics, context.CancellationToken);

        if (result.Reserved.Count > 0)
        {
            await context.PublishIntegrationEventAsync(BuildStockReservedEvent(order, result.Reserved), context.CancellationToken);
        }

        foreach (var rejection in result.Rejections)
        {
            await context.PublishIntegrationEventAsync(BuildStockRejectedEvent(order, rejection), context.CancellationToken);
        }
    }

    private static StockReservedEvent BuildStockReservedEvent(OrderCreatedEvent order, IReadOnlyList<StockReservation> reserved) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = nameof(StockReservedEvent),
        OccurredAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = order.CorrelationId,
        ExperimentRunId = order.ExperimentRunId,
        OrderId = order.OrderId,
        Reservations = reserved.Select(r => new Norn.Shop.Contracts.Events.StockReservation { ProductId = r.ProductId, Quantity = r.Quantity }).ToList(),
    };

    private static StockRejectedEvent BuildStockRejectedEvent(OrderCreatedEvent order, StockRejection rejection) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = nameof(StockRejectedEvent),
        OccurredAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = order.CorrelationId,
        ExperimentRunId = order.ExperimentRunId,
        OrderId = order.OrderId,
        ProductId = rejection.ProductId,
        Available = rejection.Available,
        Requested = rejection.Requested,
    };
}
