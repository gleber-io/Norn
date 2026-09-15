using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Norn.Monitor;
using Norn.Monitor.Prometheus;
using Shouldly;
using Xunit;

namespace Norn.Platform.IntegrationTests.Monitor;

/// <summary>
/// Monitor contra Prometheus real via Testcontainers (tarefa 10 da Fase 7) — sem módulo dedicado
/// no Testcontainers para o Prometheus, então o container genérico com a imagem oficial. Cobre o
/// contrato HTTP real (forma da resposta JSON), não a série de dados: sem scrape configurado, o
/// próprio Prometheus responde "success" com resultado vazio para qualquer consulta, o que já
/// valida que o parsing de <see cref="PrometheusMetricSource"/> não depende de suposição
/// desalinhada com a API real.
/// </summary>
public sealed class PrometheusMetricSourceTests : IAsyncLifetime
{
    private readonly IContainer prometheusContainer = new ContainerBuilder("prom/prometheus:latest")
        .WithPortBinding(9090, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPath("/-/ready")
            .ForPort(9090)
            .ForStatusCode(HttpStatusCode.OK)))
        .Build();

    public async ValueTask InitializeAsync() => await prometheusContainer.StartAsync();

    public async ValueTask DisposeAsync() => await prometheusContainer.DisposeAsync();

    [Fact]
    public async Task QueryInstantAsync_AgainstRealPrometheusWithoutScrapeTargets_ReturnsNullWithoutThrowing()
    {
        var metricSource = CreateMetricSource();

        var sample = await metricSource.QueryInstantAsync("up", TestContext.Current.CancellationToken);

        sample.ShouldBeNull();
    }

    [Fact]
    public async Task QueryRangeAsync_AgainstRealPrometheusWithoutScrapeTargets_ReturnsEmptyWithoutThrowing()
    {
        var metricSource = CreateMetricSource();
        var now = DateTimeOffset.UtcNow;

        var samples = await metricSource.QueryRangeAsync("up", now.AddMinutes(-5), now, TestContext.Current.CancellationToken);

        samples.ShouldBeEmpty();
    }

    private PrometheusMetricSource CreateMetricSource()
    {
        var baseUrl = new UriBuilder("http", prometheusContainer.Hostname, prometheusContainer.GetMappedPublicPort(9090)).Uri;
        var httpClient = new HttpClient { BaseAddress = baseUrl };
        var options = Options.Create(new MonitorOptions { PrometheusBaseUrl = baseUrl.ToString() });

        return new PrometheusMetricSource(httpClient, options);
    }
}
