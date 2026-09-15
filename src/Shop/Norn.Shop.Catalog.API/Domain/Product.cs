namespace Norn.Shop.Catalog.API.Domain;

/// <summary>
/// Regra de negócio: nunca reservar abaixo do estoque disponível. É invariante de domínio,
/// não `if` dentro do handler (Fase 2, tarefa 2) — <see cref="TryReserveStock"/> é o único
/// caminho que muda <see cref="Stock"/>.
/// </summary>
public sealed class Product
{
    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; }

    public string Category { get; private set; }

    public int Stock { get; private set; }

    private Product()
    {
        Name = null!;
        Description = null!;
        Currency = null!;
        Category = null!;
    }

    public Product(Guid id, string name, string description, decimal price, string currency, string category, int stock)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do produto é obrigatório.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Preço não pode ser negativo.");
        }

        if (stock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stock), stock, "Estoque não pode ser negativo.");
        }

        Id = id;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        Category = category;
        Stock = stock;
    }

    /// <summary>Reserva <paramref name="quantity"/> unidades. Retorna falso sem efeito colateral se o estoque não cobre.</summary>
    public bool TryReserveStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantidade deve ser positiva.");
        }

        if (quantity > Stock)
        {
            return false;
        }

        Stock -= quantity;
        return true;
    }
}
