using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Monitor.Prometheus;

namespace Norn.Monitor;

/// <summary>
/// Polling configurável da assinatura fechada M = 7 (tarefa 2). Um ciclo por
/// <see cref="MonitorOptions.PollInterval"/>: consulta instantânea por (serviço × métrica) e
/// empilha no <see cref="IMetricSampleBuffer"/>, que o Norn.Worker drena para alimentar o
/// Analyzer (tarefa 4b — amostras entregues continuamente, nunca <c>Fit()</c> por amostra).
/// </summary>
internal sealed partial class MonitorPollingBackgroundService(
    IMetricSource metricSource,
    IMetricSampleBuffer buffer,
    IOptions<MonitorOptions> options,
    ILogger<MonitorPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);

        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var query in PrometheusQueryCatalog.SignatureQueries())
        {
            try
            {
                var sample = await metricSource.QueryInstantAsync(query.PromQl, cancellationToken);
                if (sample is not null)
                {
                    // Nome lógico e serviço vêm do catálogo, não dos rótulos da resposta: expressões
                    // agregadas (histogram_quantile, sum, divisão) removem __name__ por definição do
                    // PromQL — usar a resposta geraria MetricName vazio. Pelo mesmo motivo, `sum by
                    // (le)`/`sum(...)/sum(...)` também removem `exported_job`, e
                    // PrometheusMetricSource.ToServiceTarget cai no default "unknown" — que quebra a
                    // leitura de topologia (Deployment "unknown" não existe) para p99, taxa de 5xx,
                    // errorsByType e latência do gateway, as quatro métricas agregadas da assinatura.
                    // Confirmado ao vivo na Fase 8 tentando fechar o loop do F3: sinal detectado com
                    // severidade Critical, contexto nunca persistido por causa do 404 do Kubernetes.
                    buffer.Append(sample with
                    {
                        MetricName = query.MetricName,
                        Target = sample.Target with { Service = query.Service },
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogQueryFailed(logger, query.MetricName, query.Service, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao consultar {MetricName} para {Service} no Prometheus.")]
    private static partial void LogQueryFailed(ILogger logger, string metricName, string service, Exception exception);
}
