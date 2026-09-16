using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Norn.Analyzer.Correlation;
using Norn.Analyzer.Detection;
using Norn.Analyzer.Settings;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Monitor;
using Norn.Monitor.Prometheus;

namespace Norn.Worker;

/// <summary>
/// Primeira metade do MAPE-K ponta a ponta (tarefas 3, 3a, 5, 6, 7 da Fase 7): drena amostras do
/// Monitor, detecta via Analyzer, correlaciona por janela, enriquece (topologia, métricas
/// recentes, flags do Shop, cooldown) e persiste sinal e contexto no Knowledge — em todos os
/// modos, inclusive <c>Observe</c> (o braço de controle precisa do mesmo registro que os demais).
/// Fora de escopo: Planner, Executor, LLM, qualquer atuação — o laço termina na persistência do
/// contexto.
/// </summary>
internal sealed partial class AnomalyPipelineBackgroundService(
    IMetricSampleBuffer sampleBuffer,
    MetricDetectorEngine detectorEngine,
    ITopologyReader topologyReader,
    RecentMetricsReader recentMetricsReader,
    IKnowledgeStore knowledgeStore,
    ICooldownStore cooldownStore,
    IPlatformConfig platformConfig,
    IFeatureFlags featureFlags,
    IOptions<AnalyzerOptions> analyzerOptions,
    IOptions<MonitorOptions> monitorOptions,
    TimeProvider timeProvider,
    ILogger<AnomalyPipelineBackgroundService> logger) : BackgroundService
{
    /// <summary>Catálogo fechado de flags do Shop (§5.4) — hoje uma única entrada.</summary>
    private static readonly string[] ShopFlagCatalog = ["payment.gateway.bypass"];

    private readonly TargetCorrelationBuffer correlationBuffer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(monitorOptions.Value.PollInterval);

        do
        {
            try
            {
                await PumpAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Uma falha transitória (K8s, Prometheus, Postgres) não pode derrubar o processo
                // que sustenta a campanha inteira — ela é registrada e o laço continua no próximo tick.
                LogPumpCycleFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        var mode = await platformConfig.GetModeAsync(cancellationToken);

        foreach (var sample in sampleBuffer.DrainNew())
        {
            foreach (var signal in detectorEngine.Observe(sample))
            {
                LogSignalDetected(logger, signal.MetricName, signal.Target.Service, signal.Severity, mode);
                await knowledgeStore.SaveAnomalySignalAsync(signal, cancellationToken);
                correlationBuffer.Add(signal, timeProvider.GetUtcNow());
            }
        }

        var closedWindows = correlationBuffer.DrainClosedWindows(timeProvider.GetUtcNow(), analyzerOptions.Value.CorrelationWindow);
        foreach (var (service, signals) in closedWindows)
        {
            await BuildAndPersistContextAsync(service, signals, cancellationToken);
        }
    }

    private async Task BuildAndPersistContextAsync(string service, IReadOnlyList<AnomalySignal> signals, CancellationToken cancellationToken)
    {
        var primaryTarget = signals[0].Target;

        var topology = await topologyReader.GetTopologyAsync(service, primaryTarget.Namespace, cancellationToken);
        var recentMetrics = await recentMetricsReader.ReadAsync(service, cancellationToken);
        var isInCooldown = await cooldownStore.IsInCooldownAsync(service, cancellationToken);
        var activeFlags = await ReadActiveFeatureFlagsAsync(cancellationToken);

        var context = ContextCorrelator.BuildContext(
            signals,
            contextId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            experimentRunId: null,
            createdAtUtc: timeProvider.GetUtcNow(),
            topology: topology,
            recentMetrics: recentMetrics,
            // Sem histórico ainda: Planner e Executor estão fora de escopo desta fase (tarefa 3).
            healingHistory: [],
            activeFeatureFlags: activeFlags,
            cooldownStatus: new CooldownStatus { IsInCooldown = isInCooldown });

        await knowledgeStore.SaveAnomalyContextAsync(context, cancellationToken);
        LogContextPersisted(logger, context.ContextId, service, context.PrimarySignal.MetricName, signals.Count);
    }

    private async Task<IReadOnlyDictionary<string, bool>> ReadActiveFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var flags = new Dictionary<string, bool>();
        foreach (var flagName in ShopFlagCatalog)
        {
            flags[flagName] = await featureFlags.IsEnabledAsync(flagName, cancellationToken);
        }

        return flags;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinal detectado: {MetricName} em {Service}, severidade {Severity} (modo {Mode}).")]
    private static partial void LogSignalDetected(ILogger logger, string metricName, string service, Severity severity, PlatformMode mode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ciclo do pipeline falhou — mantendo o laço vivo para o próximo tick.")]
    private static partial void LogPumpCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "AnomalyContext {ContextId} persistido para {Service} — primário {PrimaryMetric}, {SignalCount} sinal(is) na janela.")]
    private static partial void LogContextPersisted(ILogger logger, Guid contextId, string service, string primaryMetric, int signalCount);
}
