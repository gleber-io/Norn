using Microsoft.Extensions.Options;
using Norn.Shop.Payment.API.Application.Ports;

namespace Norn.Shop.Payment.API.Infrastructure.Gateway;

/// <summary>
/// Simulador de gateway externo (Fase 3, tarefa 3): latência configurável via
/// <see cref="PaymentGatewayOptions.LatencyMilliseconds"/> — a alavanca do cenário F3 — e recusa
/// determinística por limite de valor, sem aleatoriedade (§3: reprodutibilidade da campanha).
/// </summary>
internal sealed class SimulatedPaymentGateway(IOptions<PaymentGatewayOptions> options) : IPaymentGateway
{
    public async Task<GatewayAuthorizationResult> AuthorizeAsync(GatewayAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var latency = options.Value.LatencyMilliseconds;
        if (latency > 0)
        {
            await Task.Delay(latency, cancellationToken);
        }

        if (request.Amount > options.Value.AuthorizationLimit)
        {
            return new GatewayAuthorizationResult(
                Approved: false,
                AuthorizationCode: null,
                ReasonCode: "AMOUNT_EXCEEDS_LIMIT",
                ReasonDescription: $"Valor {request.Amount} {request.Currency} acima do limite de autorização ({options.Value.AuthorizationLimit}).");
        }

        return new GatewayAuthorizationResult(
            Approved: true,
            AuthorizationCode: Guid.NewGuid().ToString("N"),
            ReasonCode: null,
            ReasonDescription: null);
    }
}
