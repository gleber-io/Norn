using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Norn.Shop.Payment.API.Application.Ports;
using NSubstitute;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Norn.Shop.Payment.API.IntegrationTests;

/// <summary>
/// Sobe PostgreSQL, RabbitMQ e Redis reais via Testcontainers (§7.2) — Redis é real, não dublê,
/// porque <c>payment.gateway.bypass</c> (§5.7) é o próprio objeto sob teste na tarefa 3a.
/// <see cref="IPaymentGateway"/> é substituído por um dublê (NSubstitute) — é a porta que a
/// tarefa 3a exige dublar para afirmar zero invocações com a flag ligada.
/// </summary>
public sealed class PaymentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public IPaymentGateway Gateway { get; } = Substitute.For<IPaymentGateway>();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitMq.StartAsync();
        await _redis.StartAsync();

        Gateway.AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayAuthorizationResult(true, "AUTH-DEFAULT", null, null));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payment"] = _postgres.GetConnectionString(),
                ["RabbitMq:ConnectionString"] = _rabbitMq.GetConnectionString(),
                ["Redis:ConnectionString"] = _redis.GetConnectionString(),
                ["Gateway:LatencyMilliseconds"] = "0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton(Gateway);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
