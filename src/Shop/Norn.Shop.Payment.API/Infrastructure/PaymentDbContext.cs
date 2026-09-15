using MassTransit;
using Microsoft.EntityFrameworkCore;
using Norn.Shop.Payment.API.Domain;

namespace Norn.Shop.Payment.API.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentTransaction> Payments => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema separado por serviço no mesmo PostgreSQL (Fase 2, riscos) — evita migrations
        // de Catalog, Order e Payment colidirem no mesmo banco.
        modelBuilder.HasDefaultSchema("payments");

        // Tabelas do outbox/inbox transacional do MassTransit (ADR-08, §11 item 4) — sem estas
        // três linhas, o BusOutboxDeliveryService falha em runtime com "Entity type not found".
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.OrderId).IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            entity.Property(p => p.Method).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.AuthorizationCode).HasMaxLength(100);
            entity.Property(p => p.DeclineReasonCode).HasMaxLength(100);
            entity.Property(p => p.DeclineReasonDescription).HasMaxLength(500);
            entity.HasIndex(p => p.OrderId);
        });
    }
}
