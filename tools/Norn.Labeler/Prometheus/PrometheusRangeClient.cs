using System.Globalization;
using System.Text.Json;
using Norn.Labeler.Detection;

namespace Norn.Labeler.Prometheus;

/// <summary>
/// Cliente mínimo da API HTTP do Prometheus (<c>/api/v1/query_range</c>), só o suficiente para o
/// rotulador reconstruir a série de taxa de erro de uma execução já terminada. Não reusa
/// <c>Norn.Monitor.Prometheus</c> porque Norn.Labeler não referencia Norn.Monitor (§4 do Master
/// Plan: só Contracts e Knowledge) — é ferramental de campanha, não parte do loop online.
/// </summary>
public sealed class PrometheusRangeClient(HttpClient httpClient)
{
    /// <summary>
    /// Devolve a primeira série (matrix) do resultado, como percentual (0-100) — o chamador decide
    /// a query (ex.: taxa de erro 5xx já multiplicada por 100). Lista vazia se a query não bateu em
    /// nenhuma série no intervalo (dado ausente, não erro).
    /// </summary>
    public async Task<IReadOnlyList<MetricSample>> QueryRangeAsync(
        string promQl, DateTimeOffset start, DateTimeOffset end, TimeSpan step, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString(promQl);
        var url = $"/api/v1/query_range?query={query}" +
                  $"&start={start.ToUnixTimeSeconds()}" +
                  $"&end={end.ToUnixTimeSeconds()}" +
                  $"&step={step.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!string.Equals(root.GetProperty("status").GetString(), "success", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Prometheus recusou a query: {root}");
        }

        var results = root.GetProperty("data").GetProperty("result");
        if (results.GetArrayLength() == 0)
        {
            return [];
        }

        var values = results[0].GetProperty("values");
        var samples = new List<MetricSample>(values.GetArrayLength());
        foreach (var pair in values.EnumerateArray())
        {
            var timestampUnix = pair[0].GetDouble();
            var rawValue = pair[1].GetString() ?? "0";
            var value = double.Parse(rawValue, CultureInfo.InvariantCulture);
            samples.Add(new MetricSample(DateTimeOffset.FromUnixTimeMilliseconds((long)(timestampUnix * 1000)), value));
        }

        return samples;
    }
}
