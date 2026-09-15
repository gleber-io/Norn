using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Único lugar que fala com o Redis e decide, a cada 1s, se algum cenário está ativo e mira este
/// serviço. Calcula a intensidade (função pura em <see cref="IChaosScenario"/>), aplica o efeito
/// (I/O em <see cref="IChaosEffect"/>) e publica <c>norn_chaos_active</c>. Servidores cujo cenário
/// ativo não os mira simplesmente não fazem nada — registro é idêntico nos três serviços do Shop.
/// </summary>
internal sealed partial class ChaosBackgroundService(
    IChaosActivationStore activationStore,
    IEnumerable<IChaosScenario> scenarios,
    IEnumerable<IChaosEffect> effects,
    ChaosRuntimeState runtimeState,
    string serviceName,
    ILogger<ChaosBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, IChaosScenario> _scenariosById = scenarios.ToDictionary(s => s.Id);
    private readonly Dictionary<string, IChaosEffect> _effectsById = effects.ToDictionary(e => e.ScenarioId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTickFailed(logger, ex);
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var activation = await activationStore.GetActiveAsync(cancellationToken);

        var effect = activation is not null
            && _scenariosById.TryGetValue(activation.ScenarioId, out var scenario)
            && scenario.TargetService == serviceName
            && _effectsById.TryGetValue(activation.ScenarioId, out var matchedEffect)
                ? matchedEffect
                : null;

        if (effect is null)
        {
            if (runtimeState.ActiveEffect is not null)
            {
                runtimeState.ActiveEffect.Reset();
                runtimeState.ActiveEffect = null;
                ChaosMetrics.Clear();
            }

            return;
        }

        if (!ReferenceEquals(runtimeState.ActiveEffect, effect))
        {
            runtimeState.ActiveEffect?.Reset();
        }

        var elapsed = DateTimeOffset.UtcNow - activation!.ActivatedAtUtc;
        var intensity = _scenariosById[activation.ScenarioId].Intensity(elapsed);

        await effect.TickAsync(activation, intensity, cancellationToken);
        ChaosMetrics.Report(activation.ScenarioId, intensity);
        runtimeState.ActiveEffect = effect;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao aplicar o tick de caos.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
