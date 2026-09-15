using MassTransit;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.ConfirmOrderPayment;

/// <summary>
/// Consumidor de <c>PaymentApproved</c> (Fase 3, tarefa 2). <c>ApprovePayment</c> só marca a
/// decisão de pagamento como conhecida — o pedido só chega a <c>Confirmed</c> quando a reserva de
/// estoque também for conhecida (<see cref="Norn.Shop.Order.API.Domain.CustomerOrder.Recompute"/>),
/// pois Catalog.API e Payment.API consomem <c>OrderCreated</c> sem garantia de ordem entre si.
/// </summary>
public sealed class PaymentApprovedConsumer(IOrderRepository repository, IOrderMetrics metrics) : IConsumer<PaymentApprovedEvent>
{
    public async Task Consume(ConsumeContext<PaymentApprovedEvent> context)
    {
        var order = await repository.GetByIdAsync(context.Message.OrderId, context.CancellationToken)
            ?? throw new InvalidOperationException($"Pedido {context.Message.OrderId} não encontrado.");

        order.ApprovePayment();

        await repository.SaveChangesAsync(context.CancellationToken);
        metrics.RecordStatusChanged(order.Status.ToString());
    }
}
