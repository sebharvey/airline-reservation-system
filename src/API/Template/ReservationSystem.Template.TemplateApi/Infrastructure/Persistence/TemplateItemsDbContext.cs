using Microsoft.EntityFrameworkCore;
using ReservationSystem.Template.TemplateApi.Models.Database;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the [template].[Items] table.
///
/// Configures the TemplateItemRecord entity mapping to match the existing SQL Server schema.
/// EF Core handles connection lifetime internally — no manual connection management required.
/// </summary>
public sealed class TemplateItemsDbContext : DbContext
{
    public TemplateItemsDbContext(DbContextOptions<TemplateItemsDbContext> options) : base(options) { }

    public DbSet<TemplateItemRecord> Items => Set<TemplateItemRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateItemRecord>(entity =>
        {
            entity.ToTable("Items", "template");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.Id).HasColumnName("Id");
            entity.Property(i => i.Name).HasColumnName("Name").IsRequired();
            entity.Property(i => i.Status).HasColumnName("Status").IsRequired();
            entity.Property(i => i.Attributes).HasColumnName("Attributes").IsRequired(false);
            entity.Property(i => i.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(i => i.UpdatedAt).HasColumnName("UpdatedAt");
        });
    }
}
