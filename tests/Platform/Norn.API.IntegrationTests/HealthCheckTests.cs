using System.Net;
using Shouldly;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>Fase 10, tarefa 7 — com Postgres e Redis reais no ar, /health/ready fica saudável.</summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class HealthCheckTests(NornApiFactory factory)
{
    [Fact]
    public async Task GetHealthReady_WithRealDependenciesUp_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealthLive_AlwaysReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
