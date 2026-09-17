using System.Net;
using System.Text;
using Norn.API.Infrastructure.Prometheus;
using Shouldly;
using Xunit;

namespace Norn.API.UnitTests.Infrastructure.Prometheus;

/// <summary>
/// Mesmo padrão de <c>Norn.Monitor.UnitTests.Prometheus.PrometheusMetricSourceTests</c> (não
/// reaproveitável daqui — ADR-17), mas cobrindo <see cref="PrometheusMetricSource.QueryRangeAsync"/>,
/// o único método que o endpoint <c>GET /api/v1/metrics/series</c> (Fase 11) de fato chama.
/// </summary>
public sealed class PrometheusMetricSourceTests
{
    [Fact]
    public async Task QueryRangeAsync_MatrixResult_ReturnsOnePointPerValue()
    {
        var json = """
            {
              "status": "success",
              "data": {
                "resultType": "matrix",
                "result": [
                  {
                    "metric": { "__name__": "dotnet_process_memory_working_set_bytes", "exported_job": "Norn.Shop.Catalog.API" },
                    "values": [[1000, "120000000"], [1005, "121000000"], [1010, "123500000"]]
                  }
                ]
              }
            }
            """;
        var metricSource = CreateMetricSource(json);

        var samples = await metricSource.QueryRangeAsync(
            "dotnet_process_memory_working_set_bytes{exported_job=\"Norn.Shop.Catalog.API\"}",
            DateTimeOffset.FromUnixTimeSeconds(1000),
            DateTimeOffset.FromUnixTimeSeconds(1010),
            TestContext.Current.CancellationToken);

        samples.Count.ShouldBe(3);
        samples[0].Value.ShouldBe(120000000);
        samples[0].MetricName.ShouldBe("dotnet_process_memory_working_set_bytes");
        samples[0].Target.Service.ShouldBe("Norn.Shop.Catalog.API");
        samples[2].Value.ShouldBe(123500000);
    }

    [Fact]
    public async Task QueryRangeAsync_EmptyResult_ReturnsEmptyList()
    {
        var json = """{ "status": "success", "data": { "resultType": "matrix", "result": [] } }""";
        var metricSource = CreateMetricSource(json);

        var samples = await metricSource.QueryRangeAsync(
            "up", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        samples.ShouldBeEmpty();
    }

    private static PrometheusMetricSource CreateMetricSource(string responseJson)
    {
        var handler = new StubHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://prometheus.invalid") };

        return new PrometheusMetricSource(httpClient);
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }
}
