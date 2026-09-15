using System.Diagnostics.Metrics;
using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Infrastructure.Telemetry;

/// <summary>Métricas de negócio <c>norn_shop_catalog_*</c> (Fase 2, tarefa 7).</summary>
public sealed class CatalogMetrics : ICatalogMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _productsCreated;
    private readonly Counter<long> _stockReservations;

    public CatalogMetrics()
    {
        _meter = new Meter("Norn.Shop.Catalog.API");
        _productsCreated = _meter.CreateCounter<long>("norn_shop_catalog_products_created_total");
        _stockReservations = _meter.CreateCounter<long>("norn_shop_catalog_stock_reservations_total");
    }

    public void RecordProductCreated() => _productsCreated.Add(1);

    public void RecordStockReservation(bool succeeded) =>
        _stockReservations.Add(1, new KeyValuePair<string, object?>("outcome", succeeded ? "reserved" : "rejected"));

    public void Dispose() => _meter.Dispose();
}
