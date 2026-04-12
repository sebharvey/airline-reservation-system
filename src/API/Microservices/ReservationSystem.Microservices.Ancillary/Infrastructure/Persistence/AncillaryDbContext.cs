using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Ancillary bounded context.
/// Maps to [bag].[BagPolicy] and [bag].[BagPricing].
/// </summary>
public sealed class AncillaryDbContext : DbContext
{
    public AncillaryDbContext(DbContextOptions<AncillaryDbContext> options) : base(options) { }

    public DbSet<BagPolicy> BagPolicies => Set<BagPolicy>();
    public DbSet<BagPricing> BagPricings => Set<BagPricing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── BagPolicy ──────────────────────────────────────────────────────────
        modelBuilder.Entity<BagPolicy>(entity =>
        {
            entity.ToTable("BagPolicy", "bag", t =>
            {
                t.HasTrigger("TR_BagPolicy_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(p => p.PolicyId);
            entity.Property(p => p.PolicyId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(p => p.CabinCode).HasColumnType("char(1)").HasMaxLength(1).IsRequired();
            entity.Property(p => p.FreeBagsIncluded).HasColumnType("tinyint").IsRequired()
                .HasConversion<byte>();
            entity.Property(p => p.MaxWeightKgPerBag).HasColumnType("tinyint").IsRequired()
                .HasConversion<byte>();
            entity.Property(p => p.IsActive).HasColumnType("bit").IsRequired();
            entity.Property(p => p.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(p => p.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(p => p.CabinCode).IsUnique().HasDatabaseName("UQ_BagPolicy_Cabin");
        });

        // ── BagPricing ─────────────────────────────────────────────────────────
        modelBuilder.Entity<BagPricing>(entity =>
        {
            entity.ToTable("BagPricing", "bag", t =>
            {
                t.HasTrigger("TR_BagPricing_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(p => p.PricingId);
            entity.Property(p => p.PricingId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(p => p.BagSequence).HasColumnType("tinyint").IsRequired()
                .HasConversion<byte>();
            entity.Property(p => p.CurrencyCode).HasColumnType("char(3)").HasMaxLength(3).IsRequired();
            entity.Property(p => p.Price).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(p => p.IsActive).HasColumnType("bit").IsRequired();
            entity.Property(p => p.ValidFrom).HasColumnType("datetime2").IsRequired();
            entity.Property(p => p.ValidTo).HasColumnType("datetime2").IsRequired(false);
            entity.Property(p => p.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(p => p.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(p => new { p.BagSequence, p.CurrencyCode }).IsUnique()
                .HasDatabaseName("UQ_BagPricing_Sequence");
        });
    }
}
