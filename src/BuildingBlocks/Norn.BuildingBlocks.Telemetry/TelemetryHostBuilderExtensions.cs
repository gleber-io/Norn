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
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddOtlpExporter());

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
