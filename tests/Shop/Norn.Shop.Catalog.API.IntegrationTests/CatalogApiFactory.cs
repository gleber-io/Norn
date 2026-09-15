using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Norn.Shop.Catalog.API.IntegrationTests;

/// <summary>
/// Sobe PostgreSQL e RabbitMQ reais via Testcontainers — sem banco/broker compartilhado, sem
/// dependência do compose local (§7.2). O app compõe MassTransit no startup, então mesmo os
/// testes focados em persistência precisam de um broker alcançável.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("norn")
        .WithUsername("norn")
        .WithPassword("norn")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("norn")
        .WithPassword("norn")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitMq.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = _postgres.GetConnectionString(),
                ["RabbitMq:ConnectionString"] = _rabbitMq.GetConnectionString(),
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
