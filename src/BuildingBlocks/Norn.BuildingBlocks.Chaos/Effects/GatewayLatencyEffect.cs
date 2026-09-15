using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Effects;

/// <summary>
/// F3 — atraso adicional por requisição, crescente com a intensidade. Não mexe em
/// <c>PaymentGatewayOptions.LatencyMilliseconds</c> (a alavanca de configuração fixa do simulador,
/// Fase 3): soma-se a ela no pipeline HTTP, sem que `SimulatedPaymentGateway` saiba que o caos existe.
/// </summary>
internal sealed class GatewayLatencyEffect(IOptions<ChaosScenarioOptions> options) : ChaosEffectBase
{
    private volatile int _currentDelayMilliseconds;

    public override string ScenarioId => ChaosScenarioIds.F3;

    public override ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken)
    {
        _currentDelayMilliseconds = (int)(intensity * options.Value.F3MaxAdditionalLatencyMilliseconds);
        return ValueTask.CompletedTask;
    }

    public override async ValueTask OnRequestAsync(HttpContext context, RequestDelegate next)
    {
        var delay = _currentDelayMilliseconds;
        if (delay > 0)
        {
            await Task.Delay(delay, context.RequestAborted);
        }

        await next(context);
    }

    public override void Reset() => _currentDelayMilliseconds = 0;
}
