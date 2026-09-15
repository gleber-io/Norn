namespace Norn.Shop.Order.API.Application.Ports;

public interface IOrderMetrics
{
    void RecordOrderCreated();

    void RecordStatusChanged(string status);
}
