using MassTransit;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.ReserveOrderStock;

/// <summary>
/// Fronteira de entrada da feature — consumidor de <c>StockReserved</c> (Fase 3, tarefa 2).
/// <c>ReserveStock</c> é idempotente e comuta livremente com a decisão de pagamento: Catalog.API
/// reserva por item (Fase 2, best-effort) e pode publicar <c>StockReserved</c> e
/// <c>StockRejected</c> para o mesmo pedido, e Payment.API decide de forma independente, sem
/// garantia de ordem — ver <see cref="Norn.Shop.Order.API.Domain.CustomerOrder"/>.
/// </summary>
public sealed class StockReservedConsumer(IOrderRepository repository, IOrderMetrics metrics) : IConsumer<StockReservedEvent>
{
    public async Task Consume(ConsumeContext<StockReservedEvent> context)
    {
        var order = await repository.GetByIdAsync(context.Message.OrderId, context.CancellationToken)
            ?? throw new InvalidOperationException($"Pedido {context.Message.OrderId} não encontrado.");

        order.ReserveStock();

        await repository.SaveChangesAsync(context.CancellationToken);
        metrics.RecordStatusChanged(order.Status.ToString());
    }
}
