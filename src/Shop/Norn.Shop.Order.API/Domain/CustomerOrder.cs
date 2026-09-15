namespace Norn.Shop.Order.API.Domain;

/// <summary>
/// A máquina de estados é <c>Domain</c> (Fase 3, tarefa 2): <c>Pending → StockReserved → Paid → Confirmed | Rejected</c>.
/// Nomeada <c>CustomerOrder</c>, não <c>Order</c>: um tipo chamado <c>Order</c> dentro do namespace
/// <c>Norn.Shop.Order.API</c> colide com o segmento de namespace de mesmo nome (CS0118).
///
/// <para>
/// <see cref="Status"/> é <b>derivado</b> de duas decisões independentes — <see cref="_stockReserved"/>
/// e <see cref="_paymentApproved"/> — em vez de avançar por um único ponteiro linear. Motivo: Catalog.API
/// reserva estoque <i>por item</i> (Fase 2, best-effort) e pode publicar <c>StockReserved</c> e
/// <c>StockRejected</c> para o mesmo pedido; e Catalog.API/Payment.API consomem <c>OrderCreated</c>
/// de forma independente, sem garantia de ordem — com <c>payment.gateway.bypass</c> (§5.7) ligada,
/// o pagamento pode ser decidido antes da reserva de estoque. Um ponteiro linear que exige o estado
/// anterior exato (ex.: "só recebo <c>PaymentApproved</c> vindo de <c>StockReserved</c>") trata a
/// segunda mensagem que chega fora de ordem como transição inválida — que o MassTransit reentrega
/// (1s/4s/16s) até exaurir e cair no fault queue, travando o pedido. Cada método de decisão é
/// idempotente e comuta livremente; <see cref="Recompute"/> converge para o mesmo estado final
/// não importa a ordem de chegada.
/// </para>
/// </summary>
public sealed class CustomerOrder
{
    private readonly List<OrderItem> _items = [];
    private bool? _stockReserved;
    private bool? _paymentApproved;

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; }

    public OrderStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    private CustomerOrder()
    {
        Currency = null!;
    }

    public CustomerOrder(Guid id, Guid customerId, string currency, IReadOnlyList<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Moeda deve ter 3 caracteres (ISO 4217).", nameof(currency));
        }

        if (items.Count == 0)
        {
            throw new ArgumentException("Pedido precisa de ao menos um item.", nameof(items));
        }

        Id = id;
        CustomerId = customerId;
        Currency = currency;
        _items.AddRange(items);
        TotalAmount = items.Sum(i => i.UnitPrice * i.Quantity);
        Status = OrderStatus.Pending;
    }

    /// <summary>Catalog.API confirmou reserva de todo o pedido — consumidor de <c>StockReserved</c>. Idempotente.</summary>
    public void ReserveStock()
    {
        if (_stockReserved.HasValue)
        {
            return;
        }

        _stockReserved = true;
        Recompute();
    }

    /// <summary>Catalog.API recusou estoque para algum item — consumidor de <c>StockRejected</c>. Idempotente.</summary>
    public void RejectStock(string reason)
    {
        if (_stockReserved == false)
        {
            return;
        }

        _stockReserved = false;
        RejectionReason = reason;
        Recompute();
    }

    /// <summary>Payment.API aprovou o pagamento — consumidor de <c>PaymentApproved</c>. Idempotente.</summary>
    public void ApprovePayment()
    {
        if (_paymentApproved.HasValue)
        {
            return;
        }

        _paymentApproved = true;
        Recompute();
    }

    /// <summary>Payment.API recusou o pagamento — consumidor de <c>PaymentDeclined</c>. Idempotente.</summary>
    public void RejectPayment(string reason)
    {
        if (_paymentApproved == false)
        {
            return;
        }

        _paymentApproved = false;
        RejectionReason ??= reason;
        Recompute();
    }

    /// <summary>
    /// Recalcula <see cref="Status"/> a partir das duas decisões conhecidas até agora. Rejeição
    /// (de qualquer lado) é absorvente. <c>Paid</c> não é observável de fora: as duas aprovações
    /// coincidindo já produzem <c>Confirmed</c> diretamente, mesma decisão de design do consumidor
    /// original (não existe evento próprio para o passo intermediário).
    /// </summary>
    private void Recompute()
    {
        if (_stockReserved == false || _paymentApproved == false)
        {
            Status = OrderStatus.Rejected;
            return;
        }

        if (_stockReserved == true && _paymentApproved == true)
        {
            Status = OrderStatus.Confirmed;
            return;
        }

        Status = _stockReserved == true ? OrderStatus.StockReserved : OrderStatus.Pending;
    }
}
