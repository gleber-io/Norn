namespace Norn.Contracts.Ports;

/// <summary>Ver ADR-05.</summary>
public enum PlatformMode
{
    Observe,
    DryRun,
    Active,
}

/// <summary>
/// Qual decisor o <c>Norn.Worker</c> usa no ciclo de planejamento (Fase 12, braços B/C — §3).
/// <c>Llm</c> é o braço B (com fallback interno para <c>RuleEngine</c> em falha, §5.5); <c>RuleEngine</c>
/// é o braço C, que nunca chama o LLM. Default <c>Llm</c> — preserva o comportamento de todas as
/// sessões anteriores à Fase 12, que não tinham este switch.
/// </summary>
public enum PlannerBackend
{
    Llm,
    RuleEngine,
}

public sealed record ForecastConfig
{
    public bool Enabled { get; init; }

    public int HorizonMinutes { get; init; } = 5;
}

/// <summary>
/// Configuração da plataforma sob <c>norn:platform:config:</c> (§5.7, ADR-16) — árvore
/// separada do catálogo de flags do Shop, para que o Norn nunca desligue o próprio preditor
/// como "ação de cura". <c>Mode</c> é lido por Norn.Worker e Norn.Executor; <c>Forecast</c>,
/// por Norn.Analyzer. Implementada por Norn.Knowledge (Fase 7).
/// </summary>
public interface IPlatformConfig
{
    Task<PlatformMode> GetModeAsync(CancellationToken cancellationToken);

    Task<ForecastConfig> GetForecastConfigAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Escrita do forecast (Fase 12, reset de estado — defensivo: garante que nenhum teste ad hoc
    /// anterior deixou a Fase 13 ligada por engano antes de uma execução da campanha de H1/H2).
    /// </summary>
    Task SetForecastConfigAsync(ForecastConfig config, CancellationToken cancellationToken);

    /// <summary>
    /// Escrita do modo (Fase 10, <c>PUT /api/v1/mode</c>; Fase 9, circuit breaker do ADR-04,
    /// barreira c — transição automática para <see cref="PlatformMode.Observe"/> após falhas
    /// consecutivas). Implementada por Norn.Knowledge (Fase 9).
    /// </summary>
    Task SetModeAsync(PlatformMode mode, CancellationToken cancellationToken);

    /// <summary>Lido por Norn.Worker uma vez por ciclo (Fase 12) — decide entre chamar o LLM ou o RuleEngine puro.</summary>
    Task<PlannerBackend> GetPlannerBackendAsync(CancellationToken cancellationToken);

    /// <summary>Escrita do braço B/C (Fase 12, <c>run-experiment.ps1</c> no reset de estado, tarefa 2a).</summary>
    Task SetPlannerBackendAsync(PlannerBackend backend, CancellationToken cancellationToken);
}
