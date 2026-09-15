namespace Norn.Shop.Payment.API.Domain;

/// <summary>Transição inválida é exceção de domínio, não <c>if</c> no handler (Fase 3, tarefa 2).</summary>
public sealed class InvalidPaymentTransitionException(PaymentStatus current)
    : InvalidOperationException($"Pagamento está em '{current}', mas a operação exige 'Pending'.")
{
    public PaymentStatus Current { get; } = current;
}
