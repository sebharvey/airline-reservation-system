using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Ancillary bounded context.
/// Maps to [bag].[BagPolicy], [bag].[BagPricing],
///         [product].[ProductGroup], [product].[Product], [product].[ProductPrice].
/// </summary>
public sealed class AncillaryDbContext : DbContext
{
    public AncillaryDbContext(DbContextOptions<AncillaryDbContext> options) : base(options) { }

    public DbSet<BagPolicy> BagPolicies => Set<BagPolicy>();
    public DbSet<BagPricing> BagPricings => Set<BagPricing>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

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

        // ── ProductGroup ───────────────────────────────────────────────────────
        modelBuilder.Entity<ProductGroup>(entity =>
        {
            entity.ToTable("ProductGroup", "product", t =>
            {
                t.HasTrigger("TR_ProductGroup_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(g => g.ProductGroupId);
            entity.Property(g => g.ProductGroupId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(g => g.Name).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(g => g.SortOrder).HasColumnType("int").IsRequired();
            entity.Property(g => g.IsActive).HasColumnType("bit").IsRequired();
            entity.Property(g => g.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(g => g.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(g => g.Name).IsUnique().HasDatabaseName("UQ_ProductGroup_Name");
        });

        // ── Product ────────────────────────────────────────────────────────────
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.ToTable("Product", "product", t =>
            {
                t.HasTrigger("TR_Product_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(p => p.ProductId);
            entity.Property(p => p.ProductId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(p => p.ProductGroupId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(p => p.Name).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(p => p.IsSegmentSpecific).HasColumnType("bit").IsRequired();
            entity.Property(p => p.SsrCode).HasColumnType("char(4)").HasMaxLength(4).IsRequired(false);
            entity.Property(p => p.ImageBase64).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(p => p.IsActive).HasColumnType("bit").IsRequired();
            entity.Property(p => p.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(p => p.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasOne<ProductGroup>()
                .WithMany()
                .HasForeignKey(p => p.ProductGroupId)
                .HasConstraintName("FK_Product_ProductGroup");

            entity.HasMany(p => p.Prices)
                .WithOne()
                .HasForeignKey(pp => pp.ProductId)
                .HasConstraintName("FK_ProductPrice_Product")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProductPrice ───────────────────────────────────────────────────────
        modelBuilder.Entity<ProductPrice>(entity =>
        {
            entity.ToTable("ProductPrice", "product", t =>
            {
                t.HasTrigger("TR_ProductPrice_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(pp => pp.PriceId);
            entity.Property(pp => pp.PriceId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            entity.Property(pp => pp.ProductId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(pp => pp.OfferId).HasColumnType("uniqueidentifier").IsRequired();
            entity.Property(pp => pp.CurrencyCode).HasColumnType("char(3)").HasMaxLength(3).IsRequired();
            entity.Property(pp => pp.Price).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(pp => pp.Tax).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(pp => pp.IsActive).HasColumnType("bit").IsRequired();
            entity.Property(pp => pp.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(pp => pp.UpdatedAt).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(pp => new { pp.ProductId, pp.CurrencyCode }).IsUnique()
                .HasDatabaseName("UQ_ProductPrice_ProductCurrency");
            entity.HasIndex(pp => pp.OfferId).IsUnique()
                .HasDatabaseName("UQ_ProductPrice_OfferId");
        });
    }
}
