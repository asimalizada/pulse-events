using Microsoft.EntityFrameworkCore;
using OrdersService.Infrastructure.Persistence.Entities;

namespace OrdersService.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.CustomerId)
                .HasColumnName("customer_id")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.EventId)
                .HasColumnName("event_id");

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .HasColumnType("text")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.PublishedAt)
                .HasColumnName("published_at");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("ix_outbox_messages_status_created_at");

            entity.HasIndex(e => e.EventId)
                .IsUnique()
                .HasDatabaseName("ux_outbox_messages_event_id");
        });
    }
}
