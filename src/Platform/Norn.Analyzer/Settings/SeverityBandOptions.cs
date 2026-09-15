namespace Norn.Analyzer.Settings;

/// <summary>
/// Bandas de severidade por métrica (ADR-14, tarefa 4a) — valor inicial da matriz de
/// rastreabilidade (docs/metrics-matrix.md), a confirmar/calibrar com dados reais de execução
/// (tarefa 5a), nunca constante definitiva.
/// </summary>
public sealed class MetricSeverityBand
{
    /// <summary>
    /// <c>true</c>: métrica com SLO — distância = observado / <see cref="ReferenceValue"/>.
    /// <c>false</c>: indicador antecedente sem SLO — desvio = (observado − <see cref="ReferenceValue"/>) / <see cref="ReferenceValue"/>.
    /// </summary>
    public required bool IsSloBased { get; init; }

    /// <summary>SLO (quando <see cref="IsSloBased"/>) ou <c>expectedValue</c> — baseline pós-warmup medida no piloto (Fase 12).</summary>
    public required double ReferenceValue { get; init; }

    public required double MediumThreshold { get; init; }

    public required double HighThreshold { get; init; }

    public required double CriticalThreshold { get; init; }
}

public sealed class SeverityBandOptions
{
    public const string SectionName = "Norn:Analyzer:SeverityBands";

    /// <summary>
    /// Valores iniciais da assinatura fechada M = 7 (docs/metrics-matrix.md, seção 1/2). SLOs são
    /// os limiares documentados (300 ms, 500 ms, 1%); baselines (<c>ReferenceValue</c> das métricas
    /// sem SLO) são placeholders até o piloto da Fase 12 medi-las — não é constante definitiva.
    /// </summary>
    public Dictionary<string, MetricSeverityBand> Bands { get; init; } = new()
    {
        ["dotnet_process_memory_working_set_bytes"] = new MetricSeverityBand
        {
            IsSloBased = false,
            ReferenceValue = 200_000_000,
            MediumThreshold = 0.25,
            HighThreshold = 0.75,
            CriticalThreshold = 1.50,
        },
        ["dotnet_gc_pause_time_seconds_total"] = new MetricSeverityBand
        {
            IsSloBased = false,
            ReferenceValue = 0.02,
            MediumThreshold = 0.25,
            HighThreshold = 0.75,
            CriticalThreshold = 1.50,
        },
        ["http_server_request_duration_seconds_bucket"] = new MetricSeverityBand
        {
            IsSloBased = true,
            ReferenceValue = 0.300,
            MediumThreshold = 0.50,
            HighThreshold = 0.80,
            CriticalThreshold = 1.00,
        },
        ["rabbitmq_queue_messages_ready"] = new MetricSeverityBand
        {
            IsSloBased = false,
            ReferenceValue = 5,
            MediumThreshold = 0.50,
            HighThreshold = 1.50,
            CriticalThreshold = 4.00,
        },
        ["norn_shop_payments_gateway_latency_ms"] = new MetricSeverityBand
        {
            IsSloBased = true,
            ReferenceValue = 500,
            MediumThreshold = 0.50,
            HighThreshold = 0.80,
            CriticalThreshold = 1.00,
        },
        ["http_server_request_duration_seconds_count"] = new MetricSeverityBand
        {
            IsSloBased = true,
            ReferenceValue = 0.01,
            MediumThreshold = 0.50,
            HighThreshold = 0.80,
            CriticalThreshold = 1.00,
        },
        ["norn_app_errors_total"] = new MetricSeverityBand
        {
            IsSloBased = false,
            ReferenceValue = 1,
            MediumThreshold = 0.25,
            HighThreshold = 1.00,
            CriticalThreshold = 3.00,
        },
    };
}
