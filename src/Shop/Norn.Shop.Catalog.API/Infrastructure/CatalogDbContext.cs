using MassTransit;
using Microsoft.EntityFrameworkCore;
using Norn.Shop.Catalog.API.Domain;

namespace Norn.Shop.Catalog.API.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema separado por serviço no mesmo PostgreSQL (Fase 2, riscos) — evita migrations
        // de Catalog, Order e Payment colidirem no mesmo banco.
        modelBuilder.HasDefaultSchema("catalog");

        // Tabelas do outbox/inbox transacional do MassTransit (ADR-08, §11 item 4) — sem estas
        // três linhas, o BusOutboxDeliveryService falha em runtime com "Entity type not found".
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(2000);
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            entity.Property(p => p.Category).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.HasIndex(p => p.Name).IsUnique();
            entity.HasIndex(p => p.Category);
        });
    }
}
