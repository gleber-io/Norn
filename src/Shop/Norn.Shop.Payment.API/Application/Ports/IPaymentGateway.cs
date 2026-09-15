namespace Norn.Shop.Payment.API.Application.Ports;

/// <summary>
/// Porta para o simulador de gateway externo (Fase 3, tarefa 3) — a alavanca do cenário F3. O
/// teste de integração obrigatório da tarefa 3a duble esta porta e afirma zero invocações quando
/// <c>payment.gateway.bypass</c> está ligada.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayAuthorizationResult> AuthorizeAsync(GatewayAuthorizationRequest request, CancellationToken cancellationToken);
}

public sealed record GatewayAuthorizationRequest(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Method);

public sealed record GatewayAuthorizationResult(bool Approved, string? AuthorizationCode, string? ReasonCode, string? ReasonDescription);
