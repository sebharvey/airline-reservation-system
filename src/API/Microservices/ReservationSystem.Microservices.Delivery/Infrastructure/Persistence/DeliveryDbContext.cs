using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Delivery bounded context.
/// Maps to [delivery].[Ticket], [delivery].[Manifest], and [delivery].[Document].
/// </summary>
public sealed class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Manifest> Manifests => Set<Manifest>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Ticket ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Ticket", "delivery", t =>
            {
                t.HasTrigger("TR_Ticket_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(t => t.TicketId);
            entity.Property(t => t.TicketId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(t => t.ETicketNumber).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(t => t.BookingReference).HasColumnType("char(6)").HasMaxLength(6).IsRequired();
            entity.Property(t => t.PassengerId).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(t => t.IsVoided).HasColumnType("bit").IsRequired();
            entity.Property(t => t.VoidedAt).HasColumnType("datetime2").IsRequired(false);
            entity.Property(t => t.TicketData).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(t => t.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(t => t.UpdatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(t => t.Version).HasColumnType("int").IsRequired();

            entity.HasIndex(t => t.ETicketNumber).IsUnique();
            entity.HasIndex(t => t.BookingReference);
        });

        // ── Manifest ───────────────────────────────────────────────────────
        modelBuilder.Entity<Manifest>(entity =>
        {
            entity.ToTable("Manifest", "delivery", t =>
            {
                t.HasTrigger("TR_Manifest_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(m => m.ManifestId);
            entity.Property(m => m.ManifestId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(m => m.TicketId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(m => m.InventoryId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(m => m.FlightNumber).HasColumnType("varchar(10)").HasMaxLength(10).IsRequired();
            entity.Property(m => m.DepartureDate).HasColumnType("date").IsRequired();
            entity.Property(m => m.AircraftType).HasColumnType("char(4)").HasMaxLength(4).IsRequired();
            entity.Property(m => m.SeatNumber).HasColumnType("varchar(5)").HasMaxLength(5).IsRequired();
            entity.Property(m => m.CabinCode).HasColumnType("char(1)").HasMaxLength(1).IsRequired();
            entity.Property(m => m.BookingReference).HasColumnType("char(6)").HasMaxLength(6).IsRequired();
            entity.Property(m => m.ETicketNumber).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(m => m.PassengerId).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(m => m.GivenName).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(m => m.Surname).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(m => m.SsrCodes).HasColumnType("nvarchar(500)").IsRequired(false);
            entity.Property(m => m.DepartureTime).HasColumnType("time").IsRequired();
            entity.Property(m => m.ArrivalTime).HasColumnType("time").IsRequired();
            entity.Property(m => m.CheckedIn).HasColumnType("bit").IsRequired();
            entity.Property(m => m.CheckedInAt).HasColumnType("datetime2").IsRequired(false);
            entity.Property(m => m.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(m => m.UpdatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(m => m.Version).HasColumnType("int").IsRequired();

            entity.HasIndex(m => new { m.InventoryId, m.SeatNumber }).IsUnique();
            entity.HasIndex(m => new { m.InventoryId, m.ETicketNumber }).IsUnique();
            entity.HasIndex(m => new { m.FlightNumber, m.DepartureDate });
            entity.HasIndex(m => m.BookingReference);
        });

        // ── Document ───────────────────────────────────────────────────────
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Document", "delivery", t =>
            {
                t.HasTrigger("TR_Document_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(d => d.DocumentId);
            entity.Property(d => d.DocumentId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(d => d.DocumentNumber).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(d => d.DocumentType).HasColumnType("varchar(30)").HasMaxLength(30).IsRequired();
            entity.Property(d => d.BookingReference).HasColumnType("char(6)").HasMaxLength(6).IsRequired();
            entity.Property(d => d.ETicketNumber).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(d => d.PassengerId).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(d => d.SegmentRef).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(d => d.PaymentReference).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(d => d.Amount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(d => d.CurrencyCode).HasColumnType("char(3)").HasMaxLength(3).IsRequired();
            entity.Property(d => d.IsVoided).HasColumnType("bit").IsRequired();
            entity.Property(d => d.DocumentData).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(d => d.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(d => d.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(d => d.DocumentNumber).IsUnique();
            entity.HasIndex(d => d.BookingReference);
            entity.HasIndex(d => d.ETicketNumber);
        });
    }
}
