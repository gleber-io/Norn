namespace Norn.Shop.Payment.API.Application.Ports;

public interface IPaymentMetrics
{
    void RecordPaymentProcessed(string outcome);

    void RecordDegradedPayment();

    void RecordGatewayLatency(double milliseconds);
}
