using Microsoft.EntityFrameworkCore;

namespace ReservationSystem.Microservices.Bags.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Bags microservice.
/// Manages BagPolicy and BagPricing entities under the [bag] schema.
/// </summary>
public sealed class BagsDbContext : DbContext
{
    public BagsDbContext(DbContextOptions<BagsDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.BagPolicy> BagPolicies => Set<Domain.Entities.BagPolicy>();
    public DbSet<Domain.Entities.BagPricing> BagPricings => Set<Domain.Entities.BagPricing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bag");

        modelBuilder.Entity<Domain.Entities.BagPolicy>(entity =>
        {
            entity.ToTable("BagPolicy");
            entity.HasKey(e => e.PolicyId);
            entity.Property(e => e.PolicyId).ValueGeneratedNever();
            entity.Property(e => e.CabinCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.FreeBagsIncluded).IsRequired();
            entity.Property(e => e.MaxWeightKgPerBag).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Domain.Entities.BagPricing>(entity =>
        {
            entity.ToTable("BagPricing");
            entity.HasKey(e => e.PricingId);
            entity.Property(e => e.PricingId).ValueGeneratedNever();
            entity.Property(e => e.CabinCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.BagNumber).IsRequired();
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }
}
