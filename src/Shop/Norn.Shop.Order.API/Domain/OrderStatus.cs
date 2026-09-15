namespace Norn.Shop.Order.API.Domain;

/// <summary>Máquina de estados da Fase 3, tarefa 2: <c>Pending → StockReserved → Paid → Confirmed | Rejected</c>.</summary>
public enum OrderStatus
{
    Pending,
    StockReserved,
    Paid,
    Confirmed,
    Rejected,
}
