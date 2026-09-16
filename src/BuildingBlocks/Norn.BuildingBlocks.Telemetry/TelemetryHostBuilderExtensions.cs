using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Norn.BuildingBlocks.Telemetry;

public static class TelemetryHostBuilderExtensions
{
    /// <summary>
    /// Configura tracing, métricas e logs via OTel (§7.5). Exporta traces e métricas por OTLP
    /// para o Collector; logs recebem correlação de trace mas não são exportados — ADR-10 mantém
    /// agregação de logs fora do orçamento, e ficam em console/arquivo.
    /// </summary>
    public static IHostApplicationBuilder AddNornTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: typeof(TelemetryHostBuilderExtensions).Assembly.GetName().Version?.ToString())
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]);

        // Correlação métrica → trace só existe se o propagador de contexto for configurado.
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
        [
            new TraceContextPropagator(),
            new BaggagePropagator(),
        ]));

        var tracesSamplingRatio = builder.Configuration.GetValue("Otel:TracesSamplingRatio", 1.0);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: typeof(TelemetryHostBuilderExtensions).Assembly.GetName().Version?.ToString())
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(tracesSamplingRatio)))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // MassTransit v8 emite spans próprios nesta ActivitySource — sem isto o trace
                // "quebra" em silêncio na travessia pelo RabbitMQ (Fase 3, riscos).
                .AddSource("MassTransit")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                // §11, item 8: Instrumentation.Runtime e Instrumentation.Process saíram do plano —
                // System.Runtime já é embutido no .NET 9+ e cobre working_set e cpu.time.
                .AddMeter("System.Runtime")
                // Convenção: o Meter de métricas de negócio do serviço leva o mesmo nome do serviço.
                .AddMeter(serviceName)
                // norn_app_errors_total (ADR-10, tarefa 5a da Fase 4) — compartilhado por toda API do Shop.
                .AddMeter("Norn.BuildingBlocks.Web")
                // norn_chaos_active (ADR-13, Fase 5) — idem, compartilhado pelas três APIs do Shop.
                .AddMeter("Norn.BuildingBlocks.Chaos")
                // norn_platform_signals_total, norn_platform_detection_latency_seconds (tarefa 8 da Fase 7).
                .AddMeter("Norn.Analyzer")
                // norn_platform_planning_latency_seconds, norn_platform_fallback_total (tarefa 8 da Fase 8).
                .AddMeter("Norn.Planner")
                // norn_platform_actions_total, norn_platform_mttr_seconds, norn_platform_mode (tarefa 9 da Fase 9).
                .AddMeter("Norn.Executor")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                // Achado do replay ao vivo da Fase 9: o padrão do SDK (60s) é mais lento que a
                // corrida entre o F1 e o OOMKilled sob o limite de memória do Catalog — o
                // Analyzer via uma leitura de RSS parada por dezenas de segundos enquanto o
                // processo já tinha estourado o limite. 5s alinha com Norn.Monitor.MonitorOptions.PollInterval.
                .AddOtlpExporter((_, readerOptions) =>
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000));

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
        });

        return builder;
    }
}
