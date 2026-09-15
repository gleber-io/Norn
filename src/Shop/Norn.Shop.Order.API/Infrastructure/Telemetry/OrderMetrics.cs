using System.Diagnostics.Metrics;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Infrastructure.Telemetry;

/// <summary>Métricas de negócio <c>norn_shop_orders_*</c> (Fase 3, tarefa 8).</summary>
public sealed class OrderMetrics : IOrderMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _statusChanged;

    public OrderMetrics()
    {
        _meter = new Meter("Norn.Shop.Order.API");
        _ordersCreated = _meter.CreateCounter<long>("norn_shop_orders_created_total");
        _statusChanged = _meter.CreateCounter<long>("norn_shop_orders_status_changed_total");
    }

    public void RecordOrderCreated() => _ordersCreated.Add(1);

    public void RecordStatusChanged(string status) =>
        _statusChanged.Add(1, new KeyValuePair<string, object?>("status", status));

    public void Dispose() => _meter.Dispose();
}
