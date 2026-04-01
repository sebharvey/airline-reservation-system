using Microsoft.EntityFrameworkCore;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.Persistence;

public sealed class RetailDbContext : DbContext
{
    public RetailDbContext(DbContextOptions<RetailDbContext> options) : base(options) { }

    public DbSet<SsrCatalogueEntry> SsrCatalogue => Set<SsrCatalogueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
