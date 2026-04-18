using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Delivery bounded context.
/// Maps to [delivery].[Ticket], [delivery].[TicketTax], [delivery].[TicketTaxCoupon],
/// and [delivery].[Document].
/// </summary>
public sealed class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketTax> TicketTaxes => Set<TicketTax>();
    public DbSet<TicketTaxCoupon> TicketTaxCoupons => Set<TicketTaxCoupon>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Ticket ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Ticket", "delivery", t =>
            {
                t.HasTrigger("TR_Ticket_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(t => t.TicketId);
            entity.Property(t => t.TicketId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(t => t.TicketNumber).HasColumnType("bigint").ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            entity.Property(t => t.BookingReference).HasColumnType("char(6)").HasMaxLength(6).IsRequired();
            entity.Property(t => t.PassengerId).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(t => t.TotalFareAmount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(t => t.Currency).HasColumnType("char(3)").HasMaxLength(3).IsRequired();
            entity.Property(t => t.TotalTaxAmount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(t => t.TotalAmount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(t => t.FareCalculation).HasColumnType("nvarchar(500)").HasMaxLength(500).IsRequired();
            entity.Property(t => t.IsVoided).HasColumnType("bit").IsRequired();
            entity.Property(t => t.VoidedAt).HasColumnType("datetime2").IsRequired(false);
            entity.Property(t => t.TicketData).HasColumnType("json").IsRequired();
            entity.Property(t => t.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(t => t.UpdatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(t => t.Version).HasColumnType("int").IsRequired();

            entity.HasIndex(t => t.TicketNumber).IsUnique();
            entity.HasIndex(t => t.BookingReference);

            // TicketTaxes navigation — EF uses the backing field _ticketTaxes via name convention.
            entity.HasMany(t => t.TicketTaxes)
                .WithOne()
                .HasForeignKey(tx => tx.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(t => t.TicketTaxes)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ── TicketTax ──────────────────────────────────────────────────────────
        modelBuilder.Entity<TicketTax>(entity =>
        {
            entity.ToTable("TicketTax", "delivery");
            entity.HasKey(tx => tx.TicketTaxId);
            entity.Property(tx => tx.TicketTaxId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(tx => tx.TicketId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(tx => tx.TaxCode).HasColumnType("varchar(4)").HasMaxLength(4).IsRequired();
            entity.Property(tx => tx.Amount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(tx => tx.Currency).HasColumnType("char(3)").HasMaxLength(3).IsRequired();

            entity.HasIndex(tx => tx.TicketId);

            entity.HasMany(tx => tx.AppliedToCoupons)
                .WithOne()
                .HasForeignKey(tc => tc.TicketTaxId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(tx => tx.AppliedToCoupons)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ── TicketTaxCoupon ────────────────────────────────────────────────────
        modelBuilder.Entity<TicketTaxCoupon>(entity =>
        {
            entity.ToTable("TicketTaxCoupon", "delivery");
            entity.HasKey(tc => tc.TicketTaxCouponId);
            entity.Property(tc => tc.TicketTaxCouponId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(tc => tc.TicketTaxId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(tc => tc.CouponNumber).HasColumnType("tinyint").IsRequired();
        });

        // ── Document ───────────────────────────────────────────────────────────
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
            entity.Property(d => d.DocumentData).HasColumnType("json").IsRequired();
            entity.Property(d => d.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(d => d.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(d => d.DocumentNumber).IsUnique();
            entity.HasIndex(d => d.BookingReference);
            entity.HasIndex(d => d.ETicketNumber);
        });
    }
}
