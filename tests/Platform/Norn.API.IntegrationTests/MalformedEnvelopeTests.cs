using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Norn.Contracts;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// Fase 10, tarefa 8 / DoD: um envelope malformado publicado em <c>norn:events</c> não derruba o
/// assinante — a próxima mensagem válida continua chegando normalmente.
/// </summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class MalformedEnvelopeTests(NornApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions PublishOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private HubConnection connection = null!;

    public async ValueTask InitializeAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/norn"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public async Task PublishMalformedThenValidEnvelope_ValidOneStillArrives()
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = connection.On<JsonElement>(PlatformEventTypes.SignalDetected, payload => received.TrySetResult(payload));

        await using var redisConnection = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var subscriber = redisConnection.GetSubscriber();

        await subscriber.PublishAsync(RedisChannel.Literal("norn:events"), "isto não é json válido {{{");

        var envelope = new PlatformEventEnvelope<object>
        {
            EventType = PlatformEventTypes.SignalDetected,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new { marker = "sobreviveu" },
        };
        var json = JsonSerializer.Serialize(envelope, PublishOptions);
        await subscriber.PublishAsync(RedisChannel.Literal("norn:events"), json);

        var payload = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
        payload.GetProperty("marker").GetString().ShouldBe("sobreviveu");
    }
}
