namespace Norn.Executor.Settings;

/// <summary>
/// Parâmetros do Executor (§5.4, ADR-04). <c>MaxReplicas</c>, <c>RestartPodCooldown</c>,
/// <c>MaxActionsPerWindow</c>, <c>ActionWindow</c> e <c>ServiceToDeploymentName</c> chegam
/// vinculados às mesmas seções <c>Norn:Planner</c> e <c>Norn:Monitor</c> que
/// <c>Norn.Planner.Settings.PlannerOptions</c> e <c>Norn.Monitor.MonitorOptions</c> já usam
/// (<see cref="ExecutorServiceCollectionExtensions"/>) — nunca um segundo valor a manter em
/// sincronia manualmente: o §5.4 é explícito que <c>maxReplicas</c> "muda-se num lugar só". Os
/// defaults abaixo replicam esses valores apenas para instanciação direta em teste, sem DI.
/// </summary>
public sealed class ExecutorOptions
{
    public const string SectionName = "Norn:Executor";

    /// <summary>Barreira do §5.4 — mesma leitura de <c>Norn:Planner:MaxReplicas</c>.</summary>
    public int MaxReplicas { get; init; } = 3;

    /// <summary>Barreira do §5.4 — mesma leitura de <c>Norn:Planner:RestartPodCooldown</c>.</summary>
    public TimeSpan RestartPodCooldown { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>ADR-04, barreira (b) — mesma leitura de <c>Norn:Planner:MaxActionsPerWindow</c>.</summary>
    public int MaxActionsPerWindow { get; init; } = 3;

    /// <summary>ADR-04, barreira (b) — mesma leitura de <c>Norn:Planner:ActionWindow</c>.</summary>
    public TimeSpan ActionWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Serviço lógico → nome do Deployment — mesma leitura de <c>Norn:Monitor:ServiceToDeploymentName</c>.</summary>
    public Dictionary<string, string> ServiceToDeploymentName { get; init; } = new()
    {
        ["Norn.Shop.Catalog.API"] = "catalog-api",
        ["Norn.Shop.Order.API"] = "order-api",
        ["Norn.Shop.Payment.API"] = "payment-api",
    };

    /// <summary>ADR-04, barreira (a) — cooldown geral por alvo após qualquer ação com efeito real.</summary>
    public TimeSpan GeneralCooldown { get; init; } = TimeSpan.FromSeconds(180);

    /// <summary>ADR-04, barreira (c) — falhas consecutivas até abrir o circuito e forçar <c>Observe</c>.</summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Namespace onde o catálogo fechado de ações atua (ADR-03) — o mesmo em toda checagem de
    /// RBAC/capacidade de startup (tarefa 1a).
    /// </summary>
    public string Namespace { get; init; } = "norn-shop";

    // Limiares de "SLO restaurado" pós-verificação (tarefa 5) — pergunta distinta da severidade de
    // detecção (Norn.Analyzer.Settings.SeverityBandOptions, ADR-14: "quão longe do SLO"): aqui é
    // "voltou a ficar bom o bastante depois da ação". Reaproveitar os mesmos números de referência
    // é coincidência de domínio (mesma métrica, mesmo SLO), não motivo para o Executor referenciar
    // Norn.Analyzer — o isolamento entre pares (ADR-17) vale também entre esses dois.
    public long MemoryRestoredThresholdBytes { get; init; } = 200_000_000;

    public double LatencyP99RestoredMs { get; init; } = 300;

    public double ErrorRateRestoredPct { get; init; } = 0.1;
}
