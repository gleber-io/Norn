namespace Norn.Shop.Payment.API.Features.ProcessPayment;

public sealed record ProcessPaymentRequest
{
    public required Guid OrderId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Method { get; init; }
}
