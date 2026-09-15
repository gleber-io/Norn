using Microsoft.Extensions.Options;
using Norn.Analyzer.Detection;
using Norn.Analyzer.Settings;
using Norn.Analyzer.SeverityCalculation;
using Norn.Analyzer.Telemetry;
using Norn.Contracts;
using Shouldly;
using Xunit;

namespace Norn.Analyzer.UnitTests.Detection;

/// <summary>Tarefa 9 — séries sintéticas: spike conhecido deve ser detectado, série estável não deve gerar sinal.</summary>
public sealed class MetricDetectorEngineTests
{
    private const string MetricName = "dotnet_process_memory_working_set_bytes";
    private static readonly ServiceTarget Target = new() { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" };

    private static MetricDetectorEngine CreateEngine()
    {
        var analyzerOptions = Options.Create(new AnalyzerOptions
        {
            Confidence = 95.0,
            PValueHistoryLength = 10,
            ChangeHistoryLength = 10,
        });
        var severityCalculator = new SeverityCalculator(new SeverityBandOptions());

        return new MetricDetectorEngine(analyzerOptions, severityCalculator, new AnalyzerMetrics());
    }

    [Fact]
    public void Observe_StableSeries_NeverProducesASignal()
    {
        var engine = CreateEngine();
        var allSignals = new List<AnomalySignal>();

        for (var i = 0; i < 40; i++)
        {
            allSignals.AddRange(engine.Observe(Sample(200_000_000 + (i % 3))));
        }

        allSignals.ShouldBeEmpty();
    }

    [Fact]
    public void Observe_KnownSpikeAfterStableWarmup_ProducesASignalOnTheSpikeSample()
    {
        var engine = CreateEngine();
        IReadOnlyList<AnomalySignal> signalsOnSpike = [];

        for (var i = 0; i < 20; i++)
        {
            engine.Observe(Sample(200_000_000));
        }

        signalsOnSpike = engine.Observe(Sample(200_000_000_000));

        signalsOnSpike.ShouldNotBeEmpty();
        signalsOnSpike.ShouldAllBe(signal => signal.MetricName == MetricName && signal.Target == Target);
    }

    private static MetricSample Sample(double value) => new()
    {
        MetricName = MetricName,
        Target = Target,
        TimestampUtc = DateTimeOffset.UtcNow,
        Value = value,
    };
}
