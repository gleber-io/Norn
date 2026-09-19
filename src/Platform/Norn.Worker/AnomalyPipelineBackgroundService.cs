using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Norn.Analyzer.Correlation;
using Norn.Analyzer.Detection;
using Norn.Analyzer.Settings;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor;
using Norn.Executor.Telemetry;
using Norn.Monitor;
using Norn.Monitor.Prometheus;
using Norn.Planner.LlmPlanning;
using Norn.Worker.Events;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Worker;

/// <summary>
/// Laço completo do MAPE-K (Fase 7 + Fase 9, tarefa 7): drena amostras do Monitor, detecta via
/// Analyzer, correlaciona por janela, enriquece e persiste sinal e contexto — em todos os modos,
/// inclusive <c>Observe</c> (o braço de controle precisa do mesmo registro que os demais) — e então
/// planeja e, fora de <c>Observe</c>, atua e verifica (ADR-05: os três modos são o mesmo binário).
/// Planejar e atuar rodam destacados do laço de polling (<see cref="PumpAsync"/>): a janela de
/// verificação do Executor chega a <see cref="HealingPlan.VerificationWindowSeconds"/> (120s por
/// padrão) e a chamada ao LLM pode levar até dois timeouts de 10s (§5.5) — bloquear
/// <see cref="ExecuteAsync"/> nisso pausaria a amostragem de métricas de todos os outros alvos pelo
/// mesmo tempo.
///
/// <see cref="targetsInFlight"/> serializa por serviço: a janela de correlação (~60s, tarefa 3 da
/// Fase 7) pode fechar de novo para o mesmo alvo antes que o disparo anterior termine de planejar e
/// atuar — sem essa barreira, duas execuções concorrentes passariam pela reavaliação de
/// pré-condição do Executor (tarefa 2 da Fase 9) antes que qualquer uma tivesse gravado cooldown
/// (só gravado depois de aplicar, <c>HealingActionExecutor.RecordSideEffectsAsync</c>), e as duas
/// aplicariam ação sobre o mesmo alvo — exatamente o loop patológico que o ADR-04 existe para
/// conter. O mesmo dicionário serve para drenar as execuções em voo no <see cref="StopAsync"/>.
/// </summary>
internal sealed partial class AnomalyPipelineBackgroundService(
    IServiceScopeFactory scopeFactory,
    IMetricSampleBuffer sampleBuffer,
    MetricDetectorEngine detectorEngine,
    ITopologyReader topologyReader,
    RecentMetricsReader recentMetricsReader,
    IKnowledgeStore knowledgeStore,
    ICooldownStore cooldownStore,
    IPlatformConfig platformConfig,
    IFeatureFlags featureFlags,
    LlmPlanner llmPlanner,
    RuleEngineImpl ruleEngine,
    HealingActionExecutor healingActionExecutor,
    ExecutorMetrics executorMetrics,
    PlatformEventPublisher eventPublisher,
    IOptions<AnalyzerOptions> analyzerOptions,
    IOptions<MonitorOptions> monitorOptions,
    TimeProvider timeProvider,
    ILogger<AnomalyPipelineBackgroundService> logger) : BackgroundService
{
    private readonly TargetCorrelationBuffer correlationBuffer = new();
    private readonly ConcurrentDictionary<string, Task> targetsInFlight = new();

    /// <summary>
    /// Suporte ao diff de <see cref="PlatformEventTypes.ModeChanged"/> (Fase 10, §0.2 do plano):
    /// só Norn.Worker publica em <c>norn:events</c>, então a mudança de modo — que pode ter sido
    /// pedida via <c>PUT /api/v1/mode</c> em Norn.API — chega ao canal pela borda detectada aqui,
    /// não por um publish dentro de <c>IPlatformConfig.SetModeAsync</c> (código compartilhado
    /// pelos dois processos, o que violaria "único publicador").
    /// </summary>
    private PlatformMode? previousMode;

    /// <summary>
    /// Drena as execuções destacadas em voo antes de o host terminar — sem isto, um shutdown no
    /// meio da janela de verificação (até 120s) derruba a task sem persistir o <see cref="HealingOutcome"/>,
    /// mesmo que a ação já tenha sido aplicada de verdade no cluster: perda silenciosa de dado de
    /// medição, não só um recurso vazando.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        var inFlight = targetsInFlight.Values.ToArray();
        if (inFlight.Length == 0)
        {
            return;
        }

        LogDrainingInFlightExecutions(logger, inFlight.Length);
        try
        {
            await Task.WhenAll(inFlight).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            LogDrainTimedOut(logger, inFlight.Length);
        }
    }

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
        executorMetrics.RecordMode(mode);

        if (previousMode is not null && previousMode != mode)
        {
            // Latência de detecção limitada ao intervalo de polling (5s por padrão) — aceitável,
            // best-effort por ADR-15. Guarda contra o primeiro tick: o dashboard recebe o modo de
            // partida via GET /api/v1/mode ao conectar, não por um evento que ninguém ouviria.
            await eventPublisher.PublishAsync(PlatformEventTypes.ModeChanged, timeProvider.GetUtcNow(), null, Guid.NewGuid(), mode, cancellationToken);
        }

        previousMode = mode;

        // Fase 12 — lido fresco a cada ciclo, mesmo padrão de Mode/PlannerBackend (nunca carregado
        // de um sinal em buffer, que poderia atravessar a fronteira entre duas execuções da
        // campanha). Achado do smoke test: sem isto, todo AnomalySignal/AnomalyContext saía com
        // ExperimentRunId nulo, e o join de Norn.PairedAnalysis (H2) nunca batia com nada.
        var experimentRunId = await platformConfig.GetCurrentExperimentRunIdAsync(cancellationToken);

        foreach (var sample in sampleBuffer.DrainNew())
        {
            foreach (var rawSignal in detectorEngine.Observe(sample))
            {
                var signal = rawSignal with { ExperimentRunId = experimentRunId };
                LogSignalDetected(logger, signal.MetricName, signal.Target.Service, signal.Severity, mode);
                await knowledgeStore.SaveAnomalySignalAsync(signal, cancellationToken);
                // correlationId: AnomalySignal não carrega um próprio — o AnomalyContext que vai
                // nascer desta janela ainda não existe agora, então usamos o signalId (§0.1 do
                // plano da Fase 10). TopologyUpdated/PlanCreated/... desta mesma cadeia usam o
                // correlationId real do AnomalyContext.
                await eventPublisher.PublishAsync(PlatformEventTypes.SignalDetected, signal.DetectedAtUtc, signal.ExperimentRunId, signal.SignalId, signal, cancellationToken);
                correlationBuffer.Add(signal, timeProvider.GetUtcNow());
            }
        }

        var closedWindows = correlationBuffer.DrainClosedWindows(timeProvider.GetUtcNow(), analyzerOptions.Value.CorrelationWindow);
        foreach (var (service, signals) in closedWindows)
        {
            var context = await BuildAndPersistContextAsync(service, signals, cancellationToken);

            DispatchPlanAndExecute(service, context, cancellationToken);
        }
    }

    /// <summary>
    /// Reserva o alvo em <see cref="targetsInFlight"/> antes de despachar — ver docstring da
    /// classe. Se o alvo já está em voo, este ciclo não dispara um segundo planejamento/atuação
    /// concorrente para o mesmo serviço; o contexto já foi persistido (auditável), e o próximo
    /// tick tenta de novo depois que o disparo em curso liberar o alvo.
    /// </summary>
    private void DispatchPlanAndExecute(string service, AnomalyContext context, CancellationToken cancellationToken)
    {
        if (!targetsInFlight.TryAdd(service, Task.CompletedTask))
        {
            LogTargetAlreadyInFlight(logger, service, context.ContextId);
            return;
        }

        // Destacado do laço de polling (ver docstring da classe) — falha aqui é registrada e
        // nunca propaga para o timer principal.
        var task = PlanExecuteAndPersistAsync(context, cancellationToken);
        targetsInFlight[service] = task;
        _ = task.ContinueWith(
            _ => targetsInFlight.TryRemove(new KeyValuePair<string, Task>(service, task)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<AnomalyContext> BuildAndPersistContextAsync(string service, IReadOnlyList<AnomalySignal> signals, CancellationToken cancellationToken)
    {
        var primaryTarget = signals[0].Target;

        var topology = await topologyReader.GetTopologyAsync(service, primaryTarget.Namespace, cancellationToken);
        var recentMetrics = await recentMetricsReader.ReadAsync(service, cancellationToken);
        var isInCooldown = await cooldownStore.IsInCooldownAsync(service, cancellationToken);
        var activeFlags = await ReadActiveFeatureFlagsAsync(cancellationToken);
        var resolvedSignals = await ResolvePodIdentityAsync(service, primaryTarget.Namespace, signals, cancellationToken);

        // Derivado do próprio sinal, não relido fresco do Redis aqui (achado do code-reviewer,
        // Fase 12): a janela de correlação (~60s) pode atravessar a fronteira entre uma execução
        // da campanha e a próxima — um valor lido no instante em que a janela fecha poderia
        // divergir do ExperimentRunId já persistido nos AnomalySignal que compõem este contexto.
        // Usar signals[0] mantém o contexto sempre consistente com os próprios sinais que o
        // formam, a mesma fonte que primaryTarget acima já usa.
        var context = ContextCorrelator.BuildContext(
            resolvedSignals,
            contextId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            experimentRunId: signals[0].ExperimentRunId,
            createdAtUtc: timeProvider.GetUtcNow(),
            topology: topology,
            recentMetrics: recentMetrics,
            // Sem histórico ainda: o Executor reavalia cooldown/contagem por porta, ao vivo, na
            // hora de atuar (Fase 9, tarefa 2) — não depende deste snapshot.
            healingHistory: [],
            activeFeatureFlags: activeFlags,
            cooldownStatus: new CooldownStatus { IsInCooldown = isInCooldown });

        await knowledgeStore.SaveAnomalyContextAsync(context, cancellationToken);
        LogContextPersisted(logger, context.ContextId, service, context.PrimarySignal.MetricName, signals.Count);

        // Publicado aqui, não logo após GetTopologyAsync: context.CorrelationId só existe depois
        // de ContextCorrelator.BuildContext retornar.
        await eventPublisher.PublishAsync(PlatformEventTypes.TopologyUpdated, context.CreatedAtUtc, context.ExperimentRunId, context.CorrelationId, context.Topology, cancellationToken);

        return context;
    }

    /// <summary>
    /// Achado do replay ao vivo da Fase 9: as métricas chegam ao Prometheus via OTel Collector sem
    /// enriquecimento <c>k8sattributes</c>, então nenhuma carrega o rótulo <c>pod</c> nativo — o
    /// <c>Target.Pod</c> vindo do Monitor é o <c>exported_instance</c> (UUID de instância OTel),
    /// deixando <c>RestartPod</c> sem UID confiável em todo contexto construído ao vivo (confirmado
    /// pelo rationale do LLM: "falta UID para RestartPod"). Resolve a identidade real do pod via
    /// <see cref="ITopologyReader.GetCurrentPodNameAsync"/> (baseline de 1 réplica por serviço,
    /// Fase 6 — sem ambiguidade) e substitui só nos sinais que alimentam este
    /// <see cref="AnomalyContext"/>; as linhas já persistidas em <c>anomaly_signals</c> (tarefa 3 da
    /// Fase 7, antes da correlação) mantêm o valor original — limitação conhecida, documentada, não
    /// resolvida aqui porque tocaria o ponto de persistência por amostra, fora do escopo deste
    /// achado. Sem pod <c>Running</c> (ex.: durante um crash loop), os sinais saem sem
    /// <c>Pod</c>/<c>PodUid</c> — o mesmo efeito seguro de antes, <c>RestartPod</c> permanece
    /// inviável, nunca uma exceção.
    /// </summary>
    private async Task<IReadOnlyList<AnomalySignal>> ResolvePodIdentityAsync(
        string service, string namespaceName, IReadOnlyList<AnomalySignal> signals, CancellationToken cancellationToken)
    {
        var podName = await topologyReader.GetCurrentPodNameAsync(service, namespaceName, cancellationToken);
        if (podName is null)
        {
            return signals;
        }

        var podUid = await topologyReader.GetPodUidAsync(service, namespaceName, podName, cancellationToken);

        return signals
            .Select(signal => signal with { Target = signal.Target with { Pod = podName, PodUid = podUid } })
            .ToList();
    }

    /// <summary>
    /// Braço B (§5.5) sempre decide; o modo (ADR-05), lido de novo aqui — pode ter mudado desde o
    /// início do ciclo, inclusive pelo próprio circuit breaker de uma execução concorrente — decide
    /// só se o Executor é chamado. <c>Observe</c> planeja e persiste o plano, nunca atua.
    ///
    /// Resolve <see cref="IKnowledgeStore"/> num escopo próprio: este serviço é singleton
    /// (<c>AddHostedService</c>) e o campo <c>knowledgeStore</c> do construtor é a mesma instância
    /// de <see cref="Microsoft.EntityFrameworkCore.DbContext"/> usada por <see cref="PumpAsync"/> —
    /// reutilizá-la aqui, com várias execuções deste método rodando destacadas e concorrentes entre
    /// si e com o próximo tick do laço de polling, violaria a garantia de uso não concorrente de um
    /// <c>DbContext</c>. Cada disparo ganha o seu.
    /// </summary>
    private async Task PlanExecuteAndPersistAsync(AnomalyContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scopedKnowledgeStore = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();

            // Braço B (Llm) chama o LlmPlanner, que cai para o RuleEngine internamente em falha
            // (§5.5) — a decisão do fallback continua contando como "decidido por Llm" na ITT.
            // Braço C (RuleEngine) nunca chama o LLM: é a única forma de medir o RuleEngine como
            // decisor de primeira linha, não só como rede de segurança (Fase 12, §3, H2).
            var backend = await platformConfig.GetPlannerBackendAsync(cancellationToken);
            var plan = backend == PlannerBackend.RuleEngine
                ? ruleEngine.Decide(context)
                : await llmPlanner.DecideAsync(context, cancellationToken);
            await scopedKnowledgeStore.SaveHealingPlanAsync(plan, cancellationToken);
            LogPlanCreated(logger, plan.PlanId, context.ContextId, plan.DecidedBy, plan.Actions.Count > 0 ? plan.Actions[0].Type : HealingActionType.NoOp);
            await eventPublisher.PublishAsync(PlatformEventTypes.PlanCreated, plan.CreatedAtUtc, context.ExperimentRunId, context.CorrelationId, plan, cancellationToken);

            var mode = await platformConfig.GetModeAsync(cancellationToken);
            if (mode == PlatformMode.Observe)
            {
                return;
            }

            var outcome = await healingActionExecutor.ExecuteAsync(plan, context.RecentMetrics, dryRun: mode == PlatformMode.DryRun, cancellationToken);
            await scopedKnowledgeStore.SaveHealingOutcomeAsync(outcome, cancellationToken);
            LogOutcomeVerified(logger, outcome.OutcomeId, plan.PlanId, outcome.Status, outcome.SloRestored);

            // O Executor aplica e verifica de forma síncrona — não existe um sinal intermediário
            // real entre os dois. ActionApplied e OutcomeVerified carregam o mesmo HealingOutcome,
            // disparados em sequência, distinguidos por occurredAtUtc (§0.3 do plano da Fase 10).
            await eventPublisher.PublishAsync(PlatformEventTypes.ActionApplied, outcome.AppliedAtUtc, context.ExperimentRunId, context.CorrelationId, outcome, cancellationToken);
            await eventPublisher.PublishAsync(PlatformEventTypes.OutcomeVerified, outcome.VerifiedAtUtc, context.ExperimentRunId, context.CorrelationId, outcome, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPlanExecuteCycleFailed(logger, context.ContextId, ex);
        }
    }

    private async Task<IReadOnlyDictionary<string, bool>> ReadActiveFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var flags = new Dictionary<string, bool>();
        foreach (var flagName in ShopFlagCatalog.All)
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

    [LoggerMessage(Level = LogLevel.Information, Message = "HealingPlan {PlanId} criado para {ContextId} (decidido por {DecidedBy}) — ação {ActionType}.")]
    private static partial void LogPlanCreated(ILogger logger, Guid planId, Guid contextId, Norn.Contracts.DecidedBy decidedBy, HealingActionType actionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "HealingOutcome {OutcomeId} para o plano {PlanId}: {Status} (SloRestored={SloRestored}).")]
    private static partial void LogOutcomeVerified(ILogger logger, Guid outcomeId, Guid planId, HealingOutcomeStatus status, bool sloRestored);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ciclo de planejamento/atuação falhou para o contexto {ContextId} — outcome não produzido para este ciclo.")]
    private static partial void LogPlanExecuteCycleFailed(ILogger logger, Guid contextId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alvo {Service} já tem planejamento/atuação em voo — contexto {ContextId} persistido, mas sem novo disparo neste ciclo (ADR-04).")]
    private static partial void LogTargetAlreadyInFlight(ILogger logger, string service, Guid contextId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Drenando {Count} execução(ões) em voo antes de encerrar.")]
    private static partial void LogDrainingInFlightExecutions(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Encerramento não esperou {Count} execução(ões) em voo terminarem — outcome pode não ter sido persistido.")]
    private static partial void LogDrainTimedOut(ILogger logger, int count);
}
