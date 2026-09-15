namespace Norn.Shop.Catalog.API.Application.Ports;

public interface ICatalogMetrics
{
    void RecordProductCreated();

    void RecordStockReservation(bool succeeded);
}
