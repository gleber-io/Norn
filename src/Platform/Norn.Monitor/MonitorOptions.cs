namespace Norn.Monitor;

/// <summary>
/// Configuração do Monitor via <c>IOptions</c> (§7.1) — nunca hardcoded. Consultas PromQL
/// parametrizadas pela matriz da Fase 4 (docs/metrics-matrix.md), polling configurável (tarefa 2).
/// </summary>
public sealed class MonitorOptions
{
    public const string SectionName = "Norn:Monitor";

    public required string PrometheusBaseUrl { get; init; }

    public string Namespace { get; init; } = "norn-shop";

    /// <summary>Intervalo de polling do <see cref="MonitorPollingBackgroundService"/>.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}
