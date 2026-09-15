using System.Diagnostics.Metrics;

namespace Norn.Analyzer.Telemetry;

/// <summary>
/// <c>norn_platform_signals_total</c> e <c>norn_platform_detection_latency_seconds</c> (tarefa 8).
/// Meter próprio — registrado pelo host (Norn.Worker) via <c>AddMeter("Norn.Analyzer")</c>.
/// </summary>
public sealed class AnalyzerMetrics : IDisposable
{
    public const string MeterName = "Norn.Analyzer";

    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> signalsTotal;
    private readonly Histogram<double> detectionLatencySeconds;

    public AnalyzerMetrics()
    {
        signalsTotal = meter.CreateCounter<long>("norn_platform_signals_total");
        detectionLatencySeconds = meter.CreateHistogram<double>("norn_platform_detection_latency_seconds");
    }

    public void RecordSignal(string service, string metricName, string detector)
    {
        signalsTotal.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("metric_name", metricName),
            new KeyValuePair<string, object?>("detector", detector));
    }

    /// <summary>Detecção: de <c>MetricSample.timestampUtc</c> até <c>AnomalySignal.detectedAtUtc</c> (decomposição da latência do loop, §3).</summary>
    public void RecordDetectionLatency(TimeSpan latency, string service, string metricName)
    {
        detectionLatencySeconds.Record(latency.TotalSeconds,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("metric_name", metricName));
    }

    public void Dispose() => meter.Dispose();
}
