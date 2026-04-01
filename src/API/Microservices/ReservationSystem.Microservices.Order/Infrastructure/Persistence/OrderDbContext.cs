using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Order bounded context.
/// Manages the [order].[Basket], [order].[Order], and [order].[SsrCatalogue] tables.
/// </summary>
public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Basket> Baskets => Set<Basket>();
    public DbSet<Domain.Entities.Order> Orders => Set<Domain.Entities.Order>();
    public DbSet<SsrCatalogueEntry> SsrCatalogue => Set<SsrCatalogueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Basket>(entity =>
        {
            entity.ToTable("Basket", "order", t =>
            {
                t.HasTrigger("TR_Basket_UpdatedAt");
            });

            entity.HasKey(b => b.BasketId);

            entity.Property(b => b.BasketId)
                  .HasColumnName("BasketId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(b => b.ChannelCode)
                  .HasColumnName("ChannelCode")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(b => b.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("char(3)")
                  .IsRequired();

            entity.Property(b => b.BasketStatus)
                  .HasColumnName("BasketStatus")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(b => b.TotalFareAmount)
                  .HasColumnName("TotalFareAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired(false);

            entity.Property(b => b.TotalSeatAmount)
                  .HasColumnName("TotalSeatAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired();

            entity.Property(b => b.TotalBagAmount)
                  .HasColumnName("TotalBagAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired();

            entity.Property(b => b.TotalAmount)
                  .HasColumnName("TotalAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired(false);

            entity.Property(b => b.ExpiresAt)
                  .HasColumnName("ExpiresAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(b => b.ConfirmedOrderId)
                  .HasColumnName("ConfirmedOrderId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired(false);

            entity.Property(b => b.Version)
                  .HasColumnName("Version")
                  .HasColumnType("int")
                  .IsRequired();

            entity.Property(b => b.BasketData)
                  .HasColumnName("BasketData")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(b => b.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(b => b.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();
        });

        modelBuilder.Entity<Domain.Entities.Order>(entity =>
        {
            entity.ToTable("Order", "order", t =>
            {
                t.HasTrigger("TR_Order_UpdatedAt");
            });

            entity.HasKey(o => o.OrderId);

            entity.Property(o => o.OrderId)
                  .HasColumnName("OrderId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(o => o.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("char(6)")
                  .IsRequired(false);

            entity.HasIndex(o => o.BookingReference)
                  .IsUnique()
                  .HasFilter("[BookingReference] IS NOT NULL");

            entity.Property(o => o.OrderStatus)
                  .HasColumnName("OrderStatus")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(o => o.ChannelCode)
                  .HasColumnName("ChannelCode")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(o => o.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("char(3)")
                  .IsRequired();

            entity.Property(o => o.TicketingTimeLimit)
                  .HasColumnName("TicketingTimeLimit")
                  .HasColumnType("datetime2")
                  .IsRequired(false);

            entity.Property(o => o.TotalAmount)
                  .HasColumnName("TotalAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired(false);

            entity.Property(o => o.Version)
                  .HasColumnName("Version")
                  .HasColumnType("int")
                  .IsRequired();

            entity.Property(o => o.OrderData)
                  .HasColumnName("OrderData")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(o => o.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(o => o.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();
        });

        modelBuilder.Entity<SsrCatalogueEntry>(entity =>
        {
            entity.ToTable("SsrCatalogue", "order", t =>
            {
                t.HasTrigger("TR_SsrCatalogue_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(e => e.SsrCatalogueId);

            entity.Property(e => e.SsrCatalogueId)
                  .HasColumnName("SsrCatalogueId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.SsrCode)
                  .HasColumnName("SsrCode")
                  .HasColumnType("char(4)")
                  .HasMaxLength(4)
                  .IsRequired();

            entity.Property(e => e.Label)
                  .HasColumnName("Label")
                  .HasColumnType("varchar(100)")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Category)
                  .HasColumnName("Category")
                  .HasColumnType("varchar(20)")
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.HasIndex(e => e.SsrCode).HasDatabaseName("IX_SsrCatalogue_Code");
        });
    }
}
