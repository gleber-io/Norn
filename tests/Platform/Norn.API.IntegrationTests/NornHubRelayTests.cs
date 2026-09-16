using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Norn.Contracts;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// Fase 10, tarefa 8 (ADR-15): publica os seis <c>eventType</c> direto no canal
/// <c>norn:events</c> — sem subir Norn.Worker — e afirma que cada um chega a um cliente SignalR
/// real conectado a <see cref="Norn.API.Hubs.NornHub"/>. Prova o mecanismo de relay isoladamente;
/// "Worker e API em processos separados" é a sessão de replay manual (plano da Fase 10, não um
/// teste automatizado).
/// </summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class NornHubRelayTests(NornApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions PublishOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(2);

    private HubConnection connection = null!;

    public async ValueTask InitializeAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/norn"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                // TestServer não suporta WebSocket real — long polling é o transporte que funciona sobre ele.
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();

    [Theory]
    [InlineData(PlatformEventTypes.SignalDetected)]
    [InlineData(PlatformEventTypes.PlanCreated)]
    [InlineData(PlatformEventTypes.ActionApplied)]
    [InlineData(PlatformEventTypes.OutcomeVerified)]
    [InlineData(PlatformEventTypes.ModeChanged)]
    [InlineData(PlatformEventTypes.TopologyUpdated)]
    public async Task PublishToChannel_ForEachEventType_ArrivesAtHubClient(string eventType)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = connection.On<JsonElement>(eventType, payload => received.TrySetResult(payload));

        await PublishAsync(eventType, new { marker = eventType });

        var payload = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
        payload.GetProperty("marker").GetString().ShouldBe(eventType);
    }

    private async Task PublishAsync(string eventType, object payload)
    {
        await using var redisConnection = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var envelope = new PlatformEventEnvelope<object>
        {
            EventType = eventType,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = payload,
        };

        var json = JsonSerializer.Serialize(envelope, PublishOptions);
        await redisConnection.GetSubscriber().PublishAsync(RedisChannel.Literal("norn:events"), json);
    }
}
