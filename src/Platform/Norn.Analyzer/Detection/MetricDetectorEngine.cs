using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using Norn.Analyzer.Settings;
using Norn.Analyzer.SeverityCalculation;
using Norn.Analyzer.Telemetry;
using Norn.Contracts;

namespace Norn.Analyzer.Detection;

/// <summary>
/// Um detector por (serviço × métrica) — <c>DetectIidSpike</c> e <c>DetectIidChangePoint</c> do
/// ML.NET.TimeSeries (tarefa 4). Predição incremental via <c>CreateTimeSeriesEngine</c> +
/// <c>Predict</c> (tarefa 4b) — nunca <c>Fit()</c> por amostra, que perderia o estado do detector
/// a cada iteração. Stateful e não thread-safe por stream: cada chave (serviço, métrica) tem seu
/// próprio par de engines, protegido por lock próprio.
/// </summary>
public sealed class MetricDetectorEngine : IDisposable
{
    private readonly MLContext mlContext = new();
    private readonly AnalyzerOptions analyzerOptions;
    private readonly SeverityCalculator severityCalculator;
    private readonly AnalyzerMetrics metrics;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<(string Service, string MetricName), MetricStreamState> streams = [];
    private readonly Lock streamsLock = new();

    public MetricDetectorEngine(
        IOptions<AnalyzerOptions> analyzerOptions,
        SeverityCalculator severityCalculator,
        AnalyzerMetrics metrics,
        TimeProvider? timeProvider = null)
    {
        this.analyzerOptions = analyzerOptions.Value;
        this.severityCalculator = severityCalculator;
        this.metrics = metrics;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Observa uma amostra e retorna 0, 1 ou 2 sinais (um por detector que disparou alerta).</summary>
    public IReadOnlyList<AnomalySignal> Observe(MetricSample sample)
    {
        var state = GetOrCreateState(sample.Target.Service, sample.MetricName);
        var signals = new List<AnomalySignal>();

        lock (state.Lock)
        {
            var spikePrediction = state.SpikeEngine.Predict(new MLSample { Value = (float)sample.Value });
            if (spikePrediction.Prediction[0] == 1.0)
            {
                signals.Add(BuildSignal(sample, DetectorType.SpikeDetection, pValue: spikePrediction.Prediction[2]));
            }

            var changePointPrediction = state.ChangePointEngine.Predict(new MLSample { Value = (float)sample.Value });
            if (changePointPrediction.Prediction[0] == 1.0)
            {
                signals.Add(BuildSignal(sample, DetectorType.ChangePointDetection, pValue: changePointPrediction.Prediction[2]));
            }
        }

        return signals;
    }

    private AnomalySignal BuildSignal(MetricSample sample, DetectorType detector, double pValue)
    {
        var (severity, expectedValue) = severityCalculator.Calculate(sample.MetricName, sample.Value);
        var detectedAtUtc = timeProvider.GetUtcNow();

        metrics.RecordSignal(sample.Target.Service, sample.MetricName, detector.ToString());
        metrics.RecordDetectionLatency(detectedAtUtc - sample.TimestampUtc, sample.Target.Service, sample.MetricName);

        return new AnomalySignal
        {
            SignalId = Guid.NewGuid(),
            DetectedAtUtc = detectedAtUtc,
            Target = sample.Target,
            MetricName = sample.MetricName,
            Detector = detector,
            Severity = severity,
            Confidence = analyzerOptions.Confidence,
            PValue = pValue,
            ObservedValue = sample.Value,
            ExpectedValue = expectedValue,
            Window = new TimeWindow
            {
                FromUtc = sample.TimestampUtc - analyzerOptions.CorrelationWindow,
                ToUtc = sample.TimestampUtc,
            },
        };
    }

    private MetricStreamState GetOrCreateState(string service, string metricName)
    {
        var key = (service, metricName);
        lock (streamsLock)
        {
            if (streams.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var created = CreateState();
            streams[key] = created;
            return created;
        }
    }

    private MetricStreamState CreateState()
    {
        var emptyData = mlContext.Data.LoadFromEnumerable(Array.Empty<MLSample>());

        var spikePipeline = mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(SpikePrediction.Prediction),
            inputColumnName: nameof(MLSample.Value),
            confidence: analyzerOptions.Confidence,
            pvalueHistoryLength: analyzerOptions.PValueHistoryLength);
        var spikeTransformer = spikePipeline.Fit(emptyData);
        var spikeEngine = spikeTransformer.CreateTimeSeriesEngine<MLSample, SpikePrediction>(mlContext);

        var changePointPipeline = mlContext.Transforms.DetectIidChangePoint(
            outputColumnName: nameof(ChangePointPrediction.Prediction),
            inputColumnName: nameof(MLSample.Value),
            confidence: analyzerOptions.Confidence,
            changeHistoryLength: analyzerOptions.ChangeHistoryLength);
        var changePointTransformer = changePointPipeline.Fit(emptyData);
        var changePointEngine = changePointTransformer.CreateTimeSeriesEngine<MLSample, ChangePointPrediction>(mlContext);

        return new MetricStreamState(spikeEngine, changePointEngine);
    }

    public void Dispose()
    {
        lock (streamsLock)
        {
            foreach (var state in streams.Values)
            {
                state.SpikeEngine.Dispose();
                state.ChangePointEngine.Dispose();
            }

            streams.Clear();
        }
    }

    private sealed class MetricStreamState(
        TimeSeriesPredictionEngine<MLSample, SpikePrediction> spikeEngine,
        TimeSeriesPredictionEngine<MLSample, ChangePointPrediction> changePointEngine)
    {
        public TimeSeriesPredictionEngine<MLSample, SpikePrediction> SpikeEngine { get; } = spikeEngine;

        public TimeSeriesPredictionEngine<MLSample, ChangePointPrediction> ChangePointEngine { get; } = changePointEngine;

        public Lock Lock { get; } = new();
    }
}
