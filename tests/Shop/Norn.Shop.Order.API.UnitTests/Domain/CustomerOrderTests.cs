using Norn.Shop.Order.API.Domain;
using Shouldly;
using Xunit;

namespace Norn.Shop.Order.API.UnitTests.Domain;

/// <summary>
/// <see cref="CustomerOrder.Status"/> é derivado de duas decisões independentes (reserva de
/// estoque e aprovação de pagamento) que podem chegar em qualquer ordem — Catalog.API e
/// Payment.API consomem <c>OrderCreated</c> sem garantia de ordem entre si, e Catalog.API reserva
/// por item (best-effort), podendo publicar <c>StockReserved</c> e <c>StockRejected</c> para o
/// mesmo pedido. Estes testes cobrem as permutações de chegada, não só o caminho feliz.
/// </summary>
public sealed class CustomerOrderTests
{
    private static CustomerOrder CreateOrder() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "BRL",
        [new OrderItem(Guid.NewGuid(), 2, 10m)]);

    [Fact]
    public void Constructor_ValidItems_ComputesTotalAmountFromItems()
    {
        var order = new CustomerOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BRL",
            [new OrderItem(Guid.NewGuid(), 2, 10m), new OrderItem(Guid.NewGuid(), 1, 5m)]);

        order.TotalAmount.ShouldBe(25m);
        order.Status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void Constructor_EmptyItems_Throws() =>
        Should.Throw<ArgumentException>(() => new CustomerOrder(Guid.NewGuid(), Guid.NewGuid(), "BRL", []));

    [Fact]
    public void Constructor_InvalidCurrency_Throws() =>
        Should.Throw<ArgumentException>(() => new CustomerOrder(Guid.NewGuid(), Guid.NewGuid(), "R$", [new OrderItem(Guid.NewGuid(), 1, 1m)]));

    [Fact]
    public void ReserveStock_Alone_TransitionsToStockReserved()
    {
        var order = CreateOrder();

        order.ReserveStock();

        order.Status.ShouldBe(OrderStatus.StockReserved);
    }

    [Fact]
    public void ReserveStock_CalledTwice_IsIdempotent()
    {
        var order = CreateOrder();

        order.ReserveStock();
        order.ReserveStock();

        order.Status.ShouldBe(OrderStatus.StockReserved);
    }

    [Fact]
    public void RejectStock_Alone_TransitionsToRejectedWithReason()
    {
        var order = CreateOrder();

        order.RejectStock("sem estoque");

        order.Status.ShouldBe(OrderStatus.Rejected);
        order.RejectionReason.ShouldBe("sem estoque");
    }

    [Fact]
    public void ApprovePayment_Alone_KeepsPendingUntilStockKnown()
    {
        // Payment.API pode decidir antes de Catalog.API reservar — o bypass (§5.7) acelera isso.
        var order = CreateOrder();

        order.ApprovePayment();

        order.Status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void ReserveStockThenApprovePayment_TransitionsToConfirmed()
    {
        var order = CreateOrder();

        order.ReserveStock();
        order.ApprovePayment();

        order.Status.ShouldBe(OrderStatus.Confirmed);
    }

    [Fact]
    public void ApprovePaymentThenReserveStock_TransitionsToConfirmed()
    {
        // Ordem invertida da saga: Payment.API decidiu primeiro (ex.: bypass rápido).
        var order = CreateOrder();

        order.ApprovePayment();
        order.ReserveStock();

        order.Status.ShouldBe(OrderStatus.Confirmed);
    }

    [Fact]
    public void RejectStockThenApprovePayment_StaysRejected()
    {
        // Pagamento aprovado para um pedido cujo estoque já foi recusado (payment.gateway.bypass
        // aprova localmente sem olhar estoque) — o pedido continua Rejected; não há caminho de
        // estorno automático do pagamento nesta fase (fora de escopo, ver "Fora de escopo" §8 Fase 3).
        var order = CreateOrder();

        order.RejectStock("sem estoque");
        order.ApprovePayment();

        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void ApprovePaymentThenRejectStock_TransitionsToRejected()
    {
        var order = CreateOrder();

        order.ApprovePayment();
        order.RejectStock("sem estoque");

        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void ReserveStockThenRejectPayment_TransitionsToRejected()
    {
        var order = CreateOrder();

        order.ReserveStock();
        order.RejectPayment("gateway recusou");

        order.Status.ShouldBe(OrderStatus.Rejected);
        order.RejectionReason.ShouldBe("gateway recusou");
    }

    [Fact]
    public void RejectPaymentThenReserveStock_StaysRejected()
    {
        var order = CreateOrder();

        order.RejectPayment("gateway recusou");
        order.ReserveStock();

        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void RejectStock_AfterAlreadyReserved_TransitionsToRejected()
    {
        // Reserva parcial (Fase 2, ReserveStockHandler): um pedido com dois itens pode gerar
        // StockReserved (para o item disponível) e StockRejected (para o item em falta).
        var order = CreateOrder();

        order.ReserveStock();
        order.RejectStock("outro item sem estoque");

        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void ReserveStock_AfterAlreadyRejected_DoesNotResurrectOrder()
    {
        var order = CreateOrder();

        order.RejectStock("item A sem estoque");
        order.ReserveStock();

        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void RejectStock_CalledTwice_KeepsFirstReason()
    {
        var order = CreateOrder();

        order.RejectStock("primeiro motivo");
        order.RejectStock("segundo motivo");

        order.RejectionReason.ShouldBe("primeiro motivo");
    }
}
