namespace Norn.Shop.Contracts.Events;

public sealed record PaymentApprovedEvent : IntegrationEvent
{
    public required Guid PaymentId { get; init; }

    public required Guid OrderId { get; init; }

    public required string AuthorizationCode { get; init; }

    public required DateTimeOffset ApprovedAtUtc { get; init; }
}
