using Norn.Shop.Catalog.API.Application.Ports;

namespace Norn.Shop.Catalog.API.Features.ReserveStock;

public static class ReserveStockHandler
{
    /// <summary>
    /// Reserva por item, best-effort: itens sem estoque suficiente entram em <c>Rejections</c>
    /// sem impedir a reserva dos demais — reflete o par StockReserved/StockRejected da §5.1,
    /// onde StockReserved carrega uma lista e StockRejected é por produto.
    /// </summary>
    public static async Task<ReserveStockResult> HandleAsync(
        ReserveStockRequest request,
        IProductRepository repository,
        ICatalogMetrics metrics,
        CancellationToken cancellationToken)
    {
        var reserved = new List<StockReservation>();
        var rejections = new List<StockRejection>();

        foreach (var item in request.Items)
        {
            var product = await repository.GetByIdAsync(item.ProductId, cancellationToken);

            if (product is null || !product.TryReserveStock(item.Quantity))
            {
                rejections.Add(new StockRejection(item.ProductId, product?.Stock ?? 0, item.Quantity));
                metrics.RecordStockReservation(succeeded: false);
                continue;
            }

            reserved.Add(new StockReservation(item.ProductId, item.Quantity));
            metrics.RecordStockReservation(succeeded: true);
        }

        if (reserved.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new ReserveStockResult(reserved, rejections);
    }
}
