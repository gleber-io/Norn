using MassTransit;
using Microsoft.EntityFrameworkCore;
using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema separado por serviço no mesmo PostgreSQL (Fase 2, riscos) — evita migrations
        // de Catalog, Order e Payment colidirem no mesmo banco.
        modelBuilder.HasDefaultSchema("orders");

        // Tabelas do outbox/inbox transacional do MassTransit (ADR-08, §11 item 4) — sem estas
        // três linhas, o BusOutboxDeliveryService falha em runtime com "Entity type not found".
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.CustomerId).IsRequired();
            entity.Property(o => o.Currency).IsRequired().HasMaxLength(3);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.RejectionReason).HasMaxLength(500);
            entity.HasIndex(o => o.CustomerId);

            // As duas decisões independentes que compõem o Status derivado (Domain/CustomerOrder.cs) —
            // sem persistir isso, cada consumo recarrega o agregado com ambas em null e "esquece"
            // uma decisão já tomada por um evento anterior.
            entity.Property<bool?>("_stockReserved").HasColumnName("StockReserved");
            entity.Property<bool?>("_paymentApproved").HasColumnName("PaymentApproved");

            entity.OwnsMany(o => o.Items, items =>
            {
                items.ToTable("order_items");
                items.WithOwner().HasForeignKey("OrderId");
                items.Property<Guid>("Id").ValueGeneratedOnAdd();
                items.HasKey("Id");
                items.Property(i => i.ProductId).IsRequired();
                items.Property(i => i.Quantity).IsRequired();
                items.Property(i => i.UnitPrice).HasPrecision(18, 2);
            });

            entity.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
