using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Norn.API.Hubs;
using Norn.Contracts;
using StackExchange.Redis;

namespace Norn.API.Events;

/// <summary>
/// Fase 10, tarefas 3/4 (ADR-15). Assina <c>norn:events</c> e repassa cada envelope ao
/// <see cref="NornHub"/> escolhendo o método por <c>eventType</c>, sem remodelar o payload.
/// Mesmo padrão de <c>Norn.Knowledge.PlatformConfigInvalidationSubscriber</c>: <see cref="IHostedService"/>
/// puro em vez de <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>, porque
/// <c>SubscribeAsync</c> registra um callback e retorna — não há laço para rodar.
///
/// Todo modo de falha (JSON inválido, envelope nulo, <c>eventType</c> desconhecido, exceção ao
/// enviar ao hub) é capturado e logado sem deixar exceção escapar do callback do
/// <c>SubscribeAsync</c> — é isso que cumpre "envelope malformado não derruba o assinante": a
/// próxima mensagem válida continua chegando normalmente.
/// </summary>
internal sealed partial class PlatformEventRelay(
    IConnectionMultiplexer connectionMultiplexer,
    IHubContext<NornHub, INornHubClient> hubContext,
    NornEventRelayHealthState healthState,
    ILogger<PlatformEventRelay> logger) : IHostedService
{
    internal const string Channel = "norn:events";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        connectionMultiplexer.ConnectionFailed += OnConnectionFailed;
        connectionMultiplexer.ConnectionRestored += OnConnectionRestored;

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(Channel), (_, message) => HandleMessage(message));
        healthState.MarkSubscribed();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        connectionMultiplexer.ConnectionFailed -= OnConnectionFailed;
        connectionMultiplexer.ConnectionRestored -= OnConnectionRestored;

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(Channel));
    }

    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs args)
    {
        healthState.MarkConnectionFailed();
        LogConnectionFailed(logger, args.Exception);
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args) => healthState.MarkConnectionRestored();

    private void HandleMessage(RedisValue message)
    {
        var raw = (string?)message;
        if (raw is null)
        {
            LogMalformedEnvelope(logger, null);
            return;
        }

        PlatformEventEnvelope<JsonElement>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PlatformEventEnvelope<JsonElement>>(raw, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            LogMalformedEnvelope(logger, ex);
            return;
        }

        if (envelope is null)
        {
            LogMalformedEnvelope(logger, null);
            return;
        }

        var dispatch = envelope.EventType switch
        {
            PlatformEventTypes.SignalDetected => () => hubContext.Clients.All.SignalDetected(envelope.Payload),
            PlatformEventTypes.PlanCreated => () => hubContext.Clients.All.PlanCreated(envelope.Payload),
            PlatformEventTypes.ActionApplied => () => hubContext.Clients.All.ActionApplied(envelope.Payload),
            PlatformEventTypes.OutcomeVerified => () => hubContext.Clients.All.OutcomeVerified(envelope.Payload),
            PlatformEventTypes.ModeChanged => () => hubContext.Clients.All.ModeChanged(envelope.Payload),
            PlatformEventTypes.TopologyUpdated => () => hubContext.Clients.All.TopologyUpdated(envelope.Payload),
            _ => (Func<Task>?)null,
        };

        if (dispatch is null)
        {
            LogUnknownEventType(logger, envelope.EventType);
            return;
        }

        _ = RelayAsync(dispatch, envelope.EventType);
    }

    private async Task RelayAsync(Func<Task> dispatch, string eventType)
    {
        try
        {
            await dispatch();
        }
        catch (Exception ex)
        {
            LogRelayFailed(logger, eventType, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conexão Redis do assinante de norn:events falhou.")]
    private static partial void LogConnectionFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Envelope malformado recebido em norn:events — descartado, assinatura continua ativa.")]
    private static partial void LogMalformedEnvelope(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "eventType desconhecido recebido em norn:events: {EventType} — descartado.")]
    private static partial void LogUnknownEventType(ILogger logger, string eventType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao repassar evento {EventType} para o NornHub.")]
    private static partial void LogRelayFailed(ILogger logger, string eventType, Exception exception);
}
