namespace Norn.Shop.Contracts;

/// <summary>
/// Envelope comum a todo evento de integração do Shop (§5.1). <c>EventId</c> é, por convenção
/// de publicação (ver Norn.BuildingBlocks.Messaging), o mesmo valor do <c>MessageId</c> do
/// MassTransit — é dele que a deduplicação do inbox depende.
/// </summary>
public abstract record IntegrationEvent
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }

    public Guid? ExperimentRunId { get; init; }
}
