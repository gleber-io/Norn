using MassTransit;
using Norn.Shop.Contracts;

namespace Norn.BuildingBlocks.Messaging;

public static class PublishEndpointExtensions
{
    /// <summary>
    /// Publica um <see cref="IntegrationEvent"/> fixando o <c>MessageId</c> do MassTransit igual
    /// a <see cref="IntegrationEvent.EventId"/> — a dedup do inbox é por MessageId (CLAUDE.md).
    /// </summary>
    public static Task PublishIntegrationEventAsync<TEvent>(
        this IPublishEndpoint publishEndpoint,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : IntegrationEvent =>
        publishEndpoint.Publish(@event, context => context.MessageId = @event.EventId, cancellationToken);
}
