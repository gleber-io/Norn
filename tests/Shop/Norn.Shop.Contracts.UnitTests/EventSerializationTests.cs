using Norn.Shop.Contracts.Events;
using Xunit;

namespace Norn.Shop.Contracts.UnitTests;

public sealed class EventSerializationTests
{
    [Fact]
    public void OrderCreatedEvent_Should_RoundTrip_ThroughJson()
    {
        var @event = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(OrderCreatedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Items = [new OrderLineItem { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 19.90m }],
            TotalAmount = 39.80m,
            Currency = "BRL",
        };

        JsonRoundTripAssert.RoundTrips(@event);
    }

    [Fact]
    public void PaymentApprovedEvent_Should_RoundTrip_ThroughJson()
    {
        var @event = new PaymentApprovedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(PaymentApprovedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            AuthorizationCode = "AUTH-123456",
            ApprovedAtUtc = DateTimeOffset.UtcNow,
        };

        JsonRoundTripAssert.RoundTrips(@event);
    }

    [Fact]
    public void PaymentDeclinedEvent_Should_RoundTrip_ThroughJson()
    {
        var @event = new PaymentDeclinedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(PaymentDeclinedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ReasonCode = "GATEWAY_TIMEOUT",
            ReasonDescription = "O gateway de pagamento não respondeu a tempo.",
        };

        JsonRoundTripAssert.RoundTrips(@event);
    }

    [Fact]
    public void StockReservedEvent_Should_RoundTrip_ThroughJson()
    {
        var @event = new StockReservedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockReservedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Reservations = [new StockReservation { ProductId = Guid.NewGuid(), Quantity = 2 }],
        };

        JsonRoundTripAssert.RoundTrips(@event);
    }

    [Fact]
    public void StockRejectedEvent_Should_RoundTrip_ThroughJson()
    {
        var @event = new StockRejectedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockRejectedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Available = 3,
            Requested = 5,
        };

        JsonRoundTripAssert.RoundTrips(@event);
    }
}
