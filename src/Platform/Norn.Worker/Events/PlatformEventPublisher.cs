using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Norn.Contracts;
using StackExchange.Redis;

namespace Norn.Worker.Events;

/// <summary>
/// O ponto único de publicação em <c>norn:events</c> (§5.6, ADR-15, Fase 10) — "O único
/// publicador é a Norn.Worker, em um ponto único de publicação. Nenhum outro processo escreve no
/// canal." <c>public</c> (não <c>internal</c>) pelo mesmo motivo que já levou
/// <c>KubernetesTopologyReader</c> a virar público na Fase 9: <c>Norn.ArchitectureTests</c>
/// precisa de <c>typeof(...).Assembly</c> a partir de fora deste projeto para provar que
/// Norn.Worker nunca referencia Norn.API.
///
/// Serializa com <see cref="JsonStringEnumConverter"/> — este é o envelope de saída para o
/// navegador, onde legibilidade importa. Não confundir com a serialização de <c>payload</c> no
/// Postgres (<see cref="Norn.Contracts.Serialization.CanonicalJson"/>), que não usa conversor de
/// enum.
///
/// Falha de publicação é capturada e logada, nunca propaga — best-effort por design (ADR-15): o
/// laço MAPE-K nunca para por causa do canal de notificação da UI.
/// </summary>
public sealed partial class PlatformEventPublisher(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<PlatformEventPublisher> logger)
{
    internal const string Channel = "norn:events";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task PublishAsync<TPayload>(
        string eventType,
        DateTimeOffset occurredAtUtc,
        Guid? runId,
        Guid correlationId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new PlatformEventEnvelope<TPayload>
            {
                EventType = eventType,
                OccurredAtUtc = occurredAtUtc,
                RunId = runId,
                CorrelationId = correlationId,
                Payload = payload,
            };

            var json = JsonSerializer.Serialize(envelope, SerializerOptions);
            await connectionMultiplexer.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), json);
        }
        catch (Exception ex)
        {
            LogPublishFailed(logger, eventType, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao publicar evento {EventType} em norn:events — laço continua normalmente.")]
    private static partial void LogPublishFailed(ILogger logger, string eventType, Exception exception);
}
