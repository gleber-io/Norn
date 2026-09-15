namespace Norn.Shop.Payment.API.Domain;

/// <summary>
/// Autorização é <c>Domain</c>: <see cref="Approve"/> e <see cref="Decline"/> só saem de
/// <see cref="PaymentStatus.Pending"/>, e a segunda tentativa de decidir o mesmo pagamento é
/// exceção, não sobrescrita silenciosa (Fase 3, tarefa 2). Nomeada <c>PaymentTransaction</c>, não
/// <c>Payment</c>: um tipo chamado <c>Payment</c> dentro do namespace <c>Norn.Shop.Payment.API</c>
/// colide com o segmento de namespace de mesmo nome (CS0118).
/// </summary>
public sealed class PaymentTransaction
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public string Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? AuthorizationCode { get; private set; }

    public string? DeclineReasonCode { get; private set; }

    public string? DeclineReasonDescription { get; private set; }

    /// <summary>Passou pelo caminho degradado de <c>payment.gateway.bypass</c> (§5.7) — fonte de <c>norn_shop_payments_degraded_total</c>.</summary>
    public bool Degraded { get; private set; }

    private PaymentTransaction()
    {
        Currency = null!;
        Method = null!;
    }

    public PaymentTransaction(Guid id, Guid orderId, decimal amount, string currency, string method)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Valor deve ser positivo.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Moeda deve ter 3 caracteres (ISO 4217).", nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Método de pagamento é obrigatório.", nameof(method));
        }

        Id = id;
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Method = method;
        Status = PaymentStatus.Pending;
    }

    public void Approve(string authorizationCode, bool degraded)
    {
        EnsurePending();
        Status = PaymentStatus.Approved;
        AuthorizationCode = authorizationCode;
        Degraded = degraded;
    }

    public void Decline(string reasonCode, string reasonDescription)
    {
        EnsurePending();
        Status = PaymentStatus.Declined;
        DeclineReasonCode = reasonCode;
        DeclineReasonDescription = reasonDescription;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidPaymentTransitionException(Status);
        }
    }
}
