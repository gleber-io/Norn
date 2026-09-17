using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.Monitor.Prometheus;

/// <summary>
/// Cliente HTTP resiliente da API do Prometheus (tarefa 2 da Fase 7). O rótulo de serviço é
/// <c>exported_job</c>, não <c>service_name</c> — o exporter <c>prometheus</c> do Collector
/// renomeia os atributos de resource porque o próprio scrape do Prometheus já ocupa
/// <c>job</c>/<c>instance</c> (docs/metrics-matrix.md).
/// </summary>
/// <summary>Público para ser testável diretamente contra um Prometheus real (Testcontainers, tarefa 10) sem carregar o resto de <c>AddNornMonitor</c> (KubernetesClient incluso).</summary>
public sealed class PrometheusMetricSource(HttpClient httpClient, IOptions<MonitorOptions> options) : IMetricSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MetricSample>> QueryRangeAsync(
        string promQlQuery,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var step = options.Value.PollInterval.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var url = $"/api/v1/query_range?query={Uri.EscapeDataString(promQlQuery)}" +
                   $"&start={ToUnixSeconds(fromUtc)}&end={ToUnixSeconds(toUtc)}&step={step}s";

        var response = await SendAsync(url, cancellationToken);
        if (response?.Data is null)
        {
            return [];
        }

        var samples = new List<MetricSample>();
        foreach (var result in response.Data.Result)
        {
            var target = ToServiceTarget(result.Metric);
            var metricName = MetricName(result.Metric);
            foreach (var point in result.Values ?? [])
            {
                if (TryParsePoint(point, out var timestamp, out var value))
                {
                    samples.Add(new MetricSample
                    {
                        MetricName = metricName,
                        Target = target,
                        TimestampUtc = timestamp,
                        Value = value,
                        Labels = result.Metric,
                    });
                }
            }
        }

        return samples;
    }

    /// <summary>
    /// Um seletor por <c>exported_job</c> pode bater em mais de uma série por um tempo depois de
    /// um <c>RestartPod</c>: o pod antigo (apagado) ainda não expirou do lookback de staleness do
    /// Prometheus, e o pod novo já publicou a primeira amostra — as duas casam o mesmo
    /// <c>exported_job</c>, com <c>exported_instance</c> diferente. Pegar <c>results[0]</c> sem
    /// critério pegava qualquer uma das duas, arbitrariamente — corrompendo a verificação de
    /// <c>SloRestored</c> especificamente para a única ação que destrói e recria a série (achado
    /// do replay ao vivo da Fase 9). A amostra mais recente por timestamp é sempre a série viva.
    /// </summary>
    public async Task<MetricSample?> QueryInstantAsync(string promQlQuery, CancellationToken cancellationToken)
    {
        var url = $"/api/v1/query?query={Uri.EscapeDataString(promQlQuery)}";
        var response = await SendAsync(url, cancellationToken);
        var results = response?.Data?.Result ?? [];

        MetricSample? freshest = null;
        foreach (var result in results)
        {
            if (result.Value is null || !TryParsePoint(result.Value, out var timestamp, out var value))
            {
                continue;
            }

            if (freshest is null || timestamp > freshest.TimestampUtc)
            {
                freshest = new MetricSample
                {
                    MetricName = MetricName(result.Metric),
                    Target = ToServiceTarget(result.Metric),
                    TimestampUtc = timestamp,
                    Value = value,
                    Labels = result.Metric,
                };
            }
        }

        return freshest;
    }

    private async Task<PrometheusResponse?> SendAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var httpResponse = await httpClient.GetAsync(relativeUrl, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PrometheusResponse>(stream, JsonOptions, cancellationToken);
    }

    private static bool TryParsePoint(object[] point, out DateTimeOffset timestampUtc, out double value)
    {
        timestampUtc = default;
        value = default;

        if (point is not [JsonElement timestampElement, JsonElement valueElement])
        {
            return false;
        }

        var unixSeconds = timestampElement.GetDouble();
        timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)(unixSeconds * 1000));

        return double.TryParse(valueElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static long ToUnixSeconds(DateTimeOffset value) => value.ToUnixTimeSeconds();

    /// <summary>Nome de métrica é o próprio <c>__name__</c> — a query PromQL já sabe qual é; cai em "" quando ausente (ex.: expressão agregada sem repassar o rótulo).</summary>
    private static string MetricName(IReadOnlyDictionary<string, string> labels) =>
        labels.GetValueOrDefault("__name__", "");

    private static ServiceTarget ToServiceTarget(IReadOnlyDictionary<string, string> labels) => new()
    {
        Service = labels.GetValueOrDefault("exported_job", "unknown"),
        Namespace = labels.GetValueOrDefault("namespace", "norn-shop"),
        Pod = labels.GetValueOrDefault("pod") ?? labels.GetValueOrDefault("exported_instance"),
    };
}
