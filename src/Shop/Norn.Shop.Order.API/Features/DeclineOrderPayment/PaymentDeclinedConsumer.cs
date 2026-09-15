using MassTransit;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.DeclineOrderPayment;

/// <summary>Consumidor de <c>PaymentDeclined</c> (Fase 3, tarefa 2): transição <c>StockReserved → Rejected</c>.</summary>
public sealed class PaymentDeclinedConsumer(IOrderRepository repository, IOrderMetrics metrics) : IConsumer<PaymentDeclinedEvent>
{
    public async Task Consume(ConsumeContext<PaymentDeclinedEvent> context)
    {
        var message = context.Message;
        var order = await repository.GetByIdAsync(message.OrderId, context.CancellationToken)
            ?? throw new InvalidOperationException($"Pedido {message.OrderId} não encontrado.");

        order.RejectPayment($"{message.ReasonCode}: {message.ReasonDescription}");

        await repository.SaveChangesAsync(context.CancellationToken);
        metrics.RecordStatusChanged(order.Status.ToString());
    }
}
