using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// Fase 11, tarefa 8 (feature <c>metrics</c>) — <c>GET /api/v1/metrics/series</c>. Substitui
/// <see cref="IMetricSource"/> por um dublê (CLAUDE.md: "teste de handler com dublê de porta,
/// nunca com Testcontainers") em vez de subir um Prometheus real — o parsing de verdade da
/// resposta HTTP do Prometheus já é coberto por
/// <c>Norn.API.UnitTests.Infrastructure.Prometheus.PrometheusMetricSourceTests</c>; este teste só
/// prova o fio elétrico completo: roteamento, binding de query string (inclusive
/// <see cref="DateTimeOffset"/>, novo neste endpoint) e validação.
/// </summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class GetMetricsSeriesEndpointTests(NornApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetMetricsSeries_ValidRequest_ReturnsSamplesFromMetricSource()
    {
        var fromUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
        var toUtc = DateTimeOffset.UtcNow;
        var samples = new List<MetricSample>
        {
            new()
            {
                MetricName = "dotnet_process_memory_working_set_bytes",
                Target = new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" },
                TimestampUtc = toUtc,
                Value = 123_000_000,
            },
        };
        var metricSource = Substitute.For<IMetricSource>();
        metricSource.QueryRangeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(samples);

        using var client = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton(metricSource)))
            .CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/metrics/series?metricName=dotnet_process_memory_working_set_bytes&service=Norn.Shop.Catalog.API&fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<MetricSample>>(JsonOptions, TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Count.ShouldBe(1);
        body[0].Value.ShouldBe(123_000_000);
    }

    [Fact]
    public async Task GetMetricsSeries_InvalidMetricName_ReturnsValidationProblem()
    {
        var client = factory.CreateClient();
        var fromUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
        var toUtc = DateTimeOffset.UtcNow;

        var response = await client.GetAsync(
            $"/api/v1/metrics/series?metricName=; DROP TABLE&service=Norn.Shop.Catalog.API&fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
