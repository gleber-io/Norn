namespace Norn.Shop.Order.API.Domain;

public sealed class OrderItem
{
    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    private OrderItem()
    {
    }

    public OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantidade deve ser positiva.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Preço unitário não pode ser negativo.");
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
