using System.Text.Json.Serialization;

namespace Norn.Monitor.Prometheus;

/// <summary>Forma da resposta HTTP da API do Prometheus (<c>/api/v1/query</c> e <c>/api/v1/query_range</c>).</summary>
internal sealed class PrometheusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("data")]
    public PrometheusData? Data { get; init; }
}

internal sealed class PrometheusData
{
    [JsonPropertyName("resultType")]
    public string ResultType { get; init; } = "";

    [JsonPropertyName("result")]
    public IReadOnlyList<PrometheusResult> Result { get; init; } = [];
}

internal sealed class PrometheusResult
{
    [JsonPropertyName("metric")]
    public IReadOnlyDictionary<string, string> Metric { get; init; } = new Dictionary<string, string>();

    /// <summary>Presente em resultado instantâneo (<c>vector</c>): <c>[timestamp, "value"]</c>.</summary>
    [JsonPropertyName("value")]
    public object[]? Value { get; init; }

    /// <summary>Presente em resultado de intervalo (<c>matrix</c>): lista de <c>[timestamp, "value"]</c>.</summary>
    [JsonPropertyName("values")]
    public IReadOnlyList<object[]>? Values { get; init; }
}
