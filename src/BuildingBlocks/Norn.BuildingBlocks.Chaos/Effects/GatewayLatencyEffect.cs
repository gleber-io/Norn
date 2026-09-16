using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Effects;

/// <summary>
/// F3 — atraso adicional crescente com a intensidade, somado a
/// <c>PaymentGatewayOptions.LatencyMilliseconds</c> (a alavanca de configuração fixa do simulador,
/// Fase 3) pelo <c>SimulatedPaymentGateway</c> do Payment.API através de
/// <see cref="IChaosGatewayDelay"/> — não mais pelo pipeline HTTP (ver o porquê no doc de
/// <see cref="IChaosGatewayDelay"/>).
/// <see cref="OnRequestAsync"/> fica como passagem pura: só existe porque a interface
/// <see cref="IChaosEffect"/> exige o método (F2 usa o dele para o limitador de concorrência).
/// </summary>
internal sealed class GatewayLatencyEffect(IOptions<ChaosScenarioOptions> options) : ChaosEffectBase, IChaosGatewayDelay
{
    private volatile int _currentDelayMilliseconds;

    public override string ScenarioId => ChaosScenarioIds.F3;

    public int CurrentAdditionalDelayMilliseconds => _currentDelayMilliseconds;

    public override ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken)
    {
        _currentDelayMilliseconds = (int)(intensity * options.Value.F3MaxAdditionalLatencyMilliseconds);
        return ValueTask.CompletedTask;
    }

    public override ValueTask OnRequestAsync(HttpContext context, RequestDelegate next) => new(next(context));

    public override void Reset() => _currentDelayMilliseconds = 0;
}
