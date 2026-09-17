using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Norn.Monitor;
using Norn.Monitor.Prometheus;
using Shouldly;
using Xunit;

namespace Norn.Monitor.UnitTests.Prometheus;

/// <summary>
/// <see cref="PrometheusMetricSource.QueryInstantAsync"/> contra respostas fabricadas — cobre o
/// achado do replay ao vivo da Fase 9: um seletor por <c>exported_job</c> pode bater em mais de
/// uma série logo após um <c>RestartPod</c> (pod antigo ainda não expirou do lookback de
/// staleness, pod novo já publicou a primeira amostra), e pegar a primeira sem critério corrompia
/// a verificação de <c>SloRestored</c> para essa ação.
/// </summary>
public sealed class PrometheusMetricSourceTests
{
    [Fact]
    public async Task QueryInstantAsync_MultipleResultsForSameSelector_ReturnsMostRecentByTimestamp()
    {
        var json = """
            {
              "status": "success",
              "data": {
                "resultType": "vector",
                "result": [
                  { "metric": { "exported_job": "Norn.Shop.Catalog.API", "exported_instance": "old-pod" }, "value": [1000, "500000000"] },
                  { "metric": { "exported_job": "Norn.Shop.Catalog.API", "exported_instance": "new-pod" }, "value": [1005, "120000000"] }
                ]
              }
            }
            """;
        var metricSource = CreateMetricSource(json);

        var sample = await metricSource.QueryInstantAsync("dotnet_process_memory_working_set_bytes", TestContext.Current.CancellationToken);

        sample.ShouldNotBeNull();
        sample.Value.ShouldBe(120000000);
        sample.Target.Pod.ShouldBe("new-pod");
    }

    [Fact]
    public async Task QueryInstantAsync_ResultsOutOfOrder_StillReturnsMostRecentByTimestamp()
    {
        var json = """
            {
              "status": "success",
              "data": {
                "resultType": "vector",
                "result": [
                  { "metric": { "exported_job": "Norn.Shop.Catalog.API", "exported_instance": "new-pod" }, "value": [1005, "120000000"] },
                  { "metric": { "exported_job": "Norn.Shop.Catalog.API", "exported_instance": "old-pod" }, "value": [1000, "500000000"] }
                ]
              }
            }
            """;
        var metricSource = CreateMetricSource(json);

        var sample = await metricSource.QueryInstantAsync("dotnet_process_memory_working_set_bytes", TestContext.Current.CancellationToken);

        sample.ShouldNotBeNull();
        sample.Value.ShouldBe(120000000);
    }

    [Fact]
    public async Task QueryInstantAsync_EmptyResult_ReturnsNull()
    {
        var json = """{ "status": "success", "data": { "resultType": "vector", "result": [] } }""";
        var metricSource = CreateMetricSource(json);

        var sample = await metricSource.QueryInstantAsync("up", TestContext.Current.CancellationToken);

        sample.ShouldBeNull();
    }

    private static PrometheusMetricSource CreateMetricSource(string responseJson)
    {
        var handler = new StubHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://prometheus.invalid") };
        var options = Options.Create(new MonitorOptions { PrometheusBaseUrl = "http://prometheus.invalid" });

        return new PrometheusMetricSource(httpClient, options);
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
