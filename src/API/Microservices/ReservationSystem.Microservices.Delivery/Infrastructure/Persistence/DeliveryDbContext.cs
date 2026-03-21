using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Delivery bounded context.
/// Manages the [delivery].[Manifest], [delivery].[Ticket], and [delivery].[Document] tables.
/// </summary>
public sealed class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Manifest> Manifests => Set<Manifest>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Manifest>(entity =>
        {
            entity.ToTable("Manifest", "delivery");

            entity.HasKey(m => m.ManifestId);

            entity.Property(m => m.ManifestId)
                  .HasColumnName("ManifestId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(m => m.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("nvarchar(6)")
                  .IsRequired();

            entity.Property(m => m.OrderId)
                  .HasColumnName("OrderId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(m => m.ManifestStatus)
                  .HasColumnName("ManifestStatus")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(m => m.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(m => m.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(m => m.ManifestData)
                  .HasColumnName("ManifestData")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Ticket", "delivery");

            entity.HasKey(t => t.TicketId);

            entity.Property(t => t.TicketId)
                  .HasColumnName("TicketId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(t => t.ManifestId)
                  .HasColumnName("ManifestId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(t => t.PassengerId)
                  .HasColumnName("PassengerId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(t => t.SegmentId)
                  .HasColumnName("SegmentId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(t => t.ETicketNumber)
                  .HasColumnName("ETicketNumber")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.Property(t => t.TicketStatus)
                  .HasColumnName("TicketStatus")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(t => t.IssuedAt)
                  .HasColumnName("IssuedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(t => t.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(t => t.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.HasIndex(t => t.ManifestId);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Document", "delivery");

            entity.HasKey(d => d.DocumentId);

            entity.Property(d => d.DocumentId)
                  .HasColumnName("DocumentId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(d => d.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("nvarchar(6)")
                  .IsRequired();

            entity.Property(d => d.OrderId)
                  .HasColumnName("OrderId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(d => d.DocumentType)
                  .HasColumnName("DocumentType")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(d => d.DocumentData)
                  .HasColumnName("DocumentData")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(d => d.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(d => d.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.HasIndex(d => d.BookingReference);
        });
    }
}
