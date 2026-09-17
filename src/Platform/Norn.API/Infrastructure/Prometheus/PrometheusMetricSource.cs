using System.Globalization;
using System.Text.Json;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Infrastructure.Prometheus;

/// <summary>
/// Implementação própria de <see cref="IMetricSource"/> para a Norn.API (tarefa 8 da Fase 11 —
/// feature <c>metrics</c>). Não referencia <c>Norn.Monitor.Prometheus.PrometheusMetricSource</c>:
/// ADR-17 proíbe Norn.API de referenciar Norn.Monitor, então este cliente HTTP cru é duplicado em
/// menor escala aqui, só com o que o endpoint de série precisa (<see cref="QueryRangeAsync"/>).
/// </summary>
public sealed class PrometheusMetricSource(HttpClient httpClient) : IMetricSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MetricSample>> QueryRangeAsync(
        string promQlQuery,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var step = Math.Max(1, (int)(toUtc - fromUtc).TotalSeconds / 250);
        var url = $"/api/v1/query_range?query={Uri.EscapeDataString(promQlQuery)}" +
                   $"&start={ToUnixSeconds(fromUtc)}&end={ToUnixSeconds(toUtc)}&step={step}s";

        using var httpResponse = await httpClient.GetAsync(url, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        var response = await JsonSerializer.DeserializeAsync<PrometheusResponse>(stream, JsonOptions, cancellationToken);
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

    /// <summary>Não usado pelo endpoint de série (só <see cref="QueryRangeAsync"/> é chamado), mas
    /// implementado para satisfazer o contrato de <see cref="IMetricSource"/> por completo.</summary>
    public async Task<MetricSample?> QueryInstantAsync(string promQlQuery, CancellationToken cancellationToken)
    {
        var url = $"/api/v1/query?query={Uri.EscapeDataString(promQlQuery)}";
        using var httpResponse = await httpClient.GetAsync(url, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        var response = await JsonSerializer.DeserializeAsync<PrometheusResponse>(stream, JsonOptions, cancellationToken);
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

    private static string MetricName(IReadOnlyDictionary<string, string> labels) =>
        labels.GetValueOrDefault("__name__", "");

    private static ServiceTarget ToServiceTarget(IReadOnlyDictionary<string, string> labels) => new()
    {
        Service = labels.GetValueOrDefault("exported_job", "unknown"),
        Namespace = labels.GetValueOrDefault("namespace", "norn-shop"),
        Pod = labels.GetValueOrDefault("pod") ?? labels.GetValueOrDefault("exported_instance"),
    };
}
