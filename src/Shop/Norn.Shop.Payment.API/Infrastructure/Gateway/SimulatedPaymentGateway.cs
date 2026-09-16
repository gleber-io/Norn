using Microsoft.Extensions.Options;
using Norn.BuildingBlocks.Chaos;
using Norn.Shop.Payment.API.Application.Ports;

namespace Norn.Shop.Payment.API.Infrastructure.Gateway;

/// <summary>
/// Simulador de gateway externo (Fase 3, tarefa 3): latência configurável via
/// <see cref="PaymentGatewayOptions.LatencyMilliseconds"/> e recusa determinística por limite de
/// valor, sem aleatoriedade (§3: reprodutibilidade da campanha). O cenário F3 (Fase 5, ADR-13)
/// soma delay adicional aqui via <see cref="IChaosGatewayDelay"/> — corrigido na Fase 8: o delay
/// vivia só no pipeline HTTP, inatingível pelo tráfego real (Order → RabbitMQ →
/// OrderCreatedConsumer nunca passa por lá) e fora da janela que
/// <c>ProcessPaymentHandler</c> cronometra em <c>norn_shop_payments_gateway_latency_ms</c>. Ver o
/// porquê completo no doc de <see cref="IChaosGatewayDelay"/>.
/// </summary>
internal sealed class SimulatedPaymentGateway(IOptions<PaymentGatewayOptions> options, IChaosGatewayDelay chaosDelay) : IPaymentGateway
{
    public async Task<GatewayAuthorizationResult> AuthorizeAsync(GatewayAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var latency = options.Value.LatencyMilliseconds + chaosDelay.CurrentAdditionalDelayMilliseconds;
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
