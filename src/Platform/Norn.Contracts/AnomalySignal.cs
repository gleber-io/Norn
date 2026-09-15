namespace Norn.Contracts;

/// <summary>Saída do Analyzer (§5.3).</summary>
public sealed record AnomalySignal
{
    public required Guid SignalId { get; init; }

    public required DateTimeOffset DetectedAtUtc { get; init; }

    public required ServiceTarget Target { get; init; }

    public required string MetricName { get; init; }

    public required DetectorType Detector { get; init; }

    public required Severity Severity { get; init; }

    /// <summary>Faixa [0, 100] — ML.NET.</summary>
    public required double Confidence { get; init; }

    public required double PValue { get; init; }

    public required double ObservedValue { get; init; }

    public required double ExpectedValue { get; init; }

    public required TimeWindow Window { get; init; }

    public Guid? ExperimentRunId { get; init; }
}
