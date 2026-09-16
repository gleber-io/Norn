namespace Norn.Contracts.Ports;

/// <summary>Ver ADR-05.</summary>
public enum PlatformMode
{
    Observe,
    DryRun,
    Active,
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
    /// Escrita do modo (Fase 10, <c>PUT /api/v1/mode</c>; Fase 9, circuit breaker do ADR-04,
    /// barreira c — transição automática para <see cref="PlatformMode.Observe"/> após falhas
    /// consecutivas). Implementada por Norn.Knowledge (Fase 9).
    /// </summary>
    Task SetModeAsync(PlatformMode mode, CancellationToken cancellationToken);
}
