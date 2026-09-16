using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// Sobe PostgreSQL e Redis reais via Testcontainers — sem infraestrutura compartilhada (§7.2). A
/// tarefa 8 da Fase 10 exige publicar direto no canal Redis sem subir o Worker: os testes que
/// usam esta fábrica conectam ao mesmo <see cref="RedisConnectionString"/> que a API assina.
///
/// <c>Redis:ConnectionString</c> é lido de forma <b>eager</b> em <c>Program.cs</c> — antes de
/// <c>WebApplicationBuilder.Build()</c>, no mesmo padrão de
/// <c>Norn.Shop.Catalog.API/Program.cs</c> — então <c>ConfigureWebHost</c>/<c>AddInMemoryCollection</c>
/// (que só se aplica dentro de <c>Build()</c>) chega tarde demais para ele. Por isso esta chave vai
/// por variável de ambiente (<c>Redis__ConnectionString</c>), lida por
/// <c>WebApplication.CreateBuilder</c> desde o início. <c>ConnectionStrings:Knowledge</c> é lida de
/// forma preguiçosa (dentro do callback do <c>AddDbContext</c>), então essa continua indo por
/// <see cref="ConfigureWebHost"/>, como em <c>CatalogApiFactory</c>.
/// </summary>
public sealed class NornApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("norn")
        .WithUsername("norn")
        .WithPassword("norn")
        .Build();

    private readonly RedisContainer redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public string RedisConnectionString => redis.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        await redis.StartAsync();

        Environment.SetEnvironmentVariable("Redis__ConnectionString", RedisConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Knowledge"] = postgres.GetConnectionString(),
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
        await redis.DisposeAsync();
        Environment.SetEnvironmentVariable("Redis__ConnectionString", null);
    }
}
