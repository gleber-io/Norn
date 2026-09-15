using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Effects;

/// <summary>
/// F2 — simula esgotamento do pool de conexões sem tocar o Npgsql (o `Norn.BuildingBlocks.Chaos`
/// não pode referenciar nenhum provedor de banco, §4): um limitador de concorrência cujo teto de
/// permits encolhe com a intensidade. Permits "reservados" ficam presos no semáforo e nunca voltam
/// enquanto a rampa sobe — a requisição que não consegue permit fica bloqueada em
/// <see cref="OnRequestAsync"/>, aumentando latência e atrasando o consumo das filas do MassTransit,
/// que compartilham o mesmo processo (e, por isso, competem pelo mesmo pool de threads/DB).
/// </summary>
internal sealed class ConcurrencyThrottleEffect(IOptions<ChaosScenarioOptions> options) : ChaosEffectBase, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(options.Value.F2BaselineConcurrency, options.Value.F2BaselineConcurrency);
    private int _reservedPermits;

    public override string ScenarioId => ChaosScenarioIds.F2;

    public override async ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken)
    {
        var baseline = options.Value.F2BaselineConcurrency;
        var floor = Math.Min(options.Value.F2MinConcurrency, baseline);
        var targetAvailable = Math.Max(floor, (int)Math.Round(baseline * (1 - intensity)));
        var targetReserved = baseline - targetAvailable;

        while (_reservedPermits < targetReserved)
        {
            await _semaphore.WaitAsync(cancellationToken);
            _reservedPermits++;
        }

        while (_reservedPermits > targetReserved)
        {
            _semaphore.Release();
            _reservedPermits--;
        }
    }

    public override async ValueTask OnRequestAsync(HttpContext context, RequestDelegate next)
    {
        await _semaphore.WaitAsync(context.RequestAborted);
        try
        {
            await next(context);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void Reset()
    {
        while (_reservedPermits > 0)
        {
            _semaphore.Release();
            _reservedPermits--;
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
