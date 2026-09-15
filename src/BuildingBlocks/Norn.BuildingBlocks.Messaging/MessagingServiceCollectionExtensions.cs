using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Norn.Shop.Contracts;
using Norn.Shop.Contracts.Events;

namespace Norn.BuildingBlocks.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Configuração compartilhada do MassTransit v8 (ADR-08): convenções de exchange/routing key
    /// da §5.1, retry com backoff, outbox/inbox EF Core. Configuração, não reimplementação —
    /// o dead-letter e a dedup do inbox por MessageId são comportamento nativo do MassTransit.
    /// </summary>
    public static IServiceCollection AddNornMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                // Testcontainers expõe RabbitMQ numa porta host aleatória — só uma URI completa
                // carrega isso; Host/Username/Password (padrão local, porta 5672) cobre o resto.
                var connectionString = configuration["RabbitMq:ConnectionString"];
                if (connectionString is not null)
                {
                    cfg.Host(new Uri(connectionString));
                }
                else
                {
                    cfg.Host(configuration["RabbitMq:Host"] ?? "localhost", h =>
                    {
                        h.Username(configuration["RabbitMq:Username"] ?? "guest");
                        h.Password(configuration["RabbitMq:Password"] ?? "guest");
                    });
                }

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(4),
                    TimeSpan.FromSeconds(16)));

                ConfigureTopic<OrderCreatedEvent>(cfg);
                ConfigureTopic<PaymentApprovedEvent>(cfg);
                ConfigureTopic<PaymentDeclinedEvent>(cfg);
                ConfigureTopic<StockReservedEvent>(cfg);
                ConfigureTopic<StockRejectedEvent>(cfg);

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static void ConfigureTopic<TEvent>(IRabbitMqBusFactoryConfigurator cfg)
        where TEvent : IntegrationEvent
    {
        cfg.Message<TEvent>(m => m.SetEntityName(NornMessagingTopology.GetExchange<TEvent>()));
        cfg.Publish<TEvent>(p => p.ExchangeType = "topic");
    }
}
