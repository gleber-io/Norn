using System.Diagnostics.Metrics;
using Norn.Shop.Payment.API.Application.Ports;

namespace Norn.Shop.Payment.API.Infrastructure.Telemetry;

/// <summary>Métricas de negócio <c>norn_shop_payments_*</c> (Fase 3, tarefa 8).</summary>
public sealed class PaymentMetrics : IPaymentMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _processed;
    private readonly Counter<long> _degraded;
    private readonly Histogram<double> _gatewayLatency;

    public PaymentMetrics()
    {
        _meter = new Meter("Norn.Shop.Payment.API");
        _processed = _meter.CreateCounter<long>("norn_shop_payments_processed_total");
        _degraded = _meter.CreateCounter<long>("norn_shop_payments_degraded_total");
        _gatewayLatency = _meter.CreateHistogram<double>("norn_shop_payments_gateway_latency_ms");
    }

    public void RecordPaymentProcessed(string outcome) =>
        _processed.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void RecordDegradedPayment() => _degraded.Add(1);

    public void RecordGatewayLatency(double milliseconds) => _gatewayLatency.Record(milliseconds);

    public void Dispose() => _meter.Dispose();
}
