namespace Norn.Shop.Payment.API.Features.ProcessPayment;

public sealed record ProcessPaymentResponse
{
    public required Guid PaymentId { get; init; }

    public required string Status { get; init; }
}
