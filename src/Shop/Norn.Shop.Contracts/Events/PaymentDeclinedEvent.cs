namespace Norn.Shop.Contracts.Events;

public sealed record PaymentDeclinedEvent : IntegrationEvent
{
    public required Guid PaymentId { get; init; }

    public required Guid OrderId { get; init; }

    public required string ReasonCode { get; init; }

    public required string ReasonDescription { get; init; }
}
