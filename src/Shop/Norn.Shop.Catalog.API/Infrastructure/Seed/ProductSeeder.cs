using Microsoft.EntityFrameworkCore;
using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Infrastructure.Seed;

/// <summary>Mínimo 500 produtos (Fase 2, tarefa 4) — volume que exercita paginação e carga de forma realista.</summary>
public static class ProductSeeder
{
    private const int ProductCount = 500;
    private const int Seed = 42;

    private static readonly string[] Categories =
        ["Eletrônicos", "Livros", "Casa", "Esporte", "Brinquedos", "Moda", "Beleza", "Alimentos"];

    private static readonly string[] Adjectives =
        ["Compacto", "Premium", "Clássico", "Portátil", "Inteligente", "Resistente", "Ergonômico", "Sustentável"];

    private static readonly string[] Nouns =
        ["Fone de Ouvido", "Cadeira", "Mochila", "Garrafa", "Notebook", "Tênis", "Câmera", "Relógio", "Livro", "Bicicleta"];

    public static async Task SeedAsync(CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var random = new Random(Seed);
        var products = new List<Product>(ProductCount);

        for (var i = 0; i < ProductCount; i++)
        {
            var adjective = Adjectives[random.Next(Adjectives.Length)];
            var noun = Nouns[random.Next(Nouns.Length)];
            var category = Categories[random.Next(Categories.Length)];
            var name = $"{noun} {adjective} #{i + 1:D4}";
            var price = Math.Round((decimal)(random.NextDouble() * 490 + 10), 2);
            var stock = random.Next(0, 200);

            products.Add(new Product(Guid.NewGuid(), name, $"{noun} {adjective}, categoria {category}.", price, "BRL", category, stock));
        }

        await dbContext.Products.AddRangeAsync(products, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
