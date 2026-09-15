using MassTransit;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.RejectOrderStock;

/// <summary>Consumidor de <c>StockRejected</c> (Fase 3, tarefa 2): transição <c>Pending → Rejected</c>.</summary>
public sealed class StockRejectedConsumer(IOrderRepository repository, IOrderMetrics metrics) : IConsumer<StockRejectedEvent>
{
    public async Task Consume(ConsumeContext<StockRejectedEvent> context)
    {
        var message = context.Message;
        var order = await repository.GetByIdAsync(message.OrderId, context.CancellationToken)
            ?? throw new InvalidOperationException($"Pedido {message.OrderId} não encontrado.");

        order.RejectStock($"Estoque insuficiente para {message.ProductId}: disponível {message.Available}, solicitado {message.Requested}.");

        await repository.SaveChangesAsync(context.CancellationToken);
        metrics.RecordStatusChanged(order.Status.ToString());
    }
}
