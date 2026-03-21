using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Seat domain tables.
/// Maps to the [seat] schema in SQL Server.
/// </summary>
public sealed class SeatDbContext : DbContext
{
    public SeatDbContext(DbContextOptions<SeatDbContext> options) : base(options) { }

    public DbSet<AircraftType> AircraftTypes => Set<AircraftType>();
    public DbSet<Seatmap> Seatmaps => Set<Seatmap>();
    public DbSet<SeatPricing> SeatPricings => Set<SeatPricing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AircraftType>(entity =>
        {
            entity.ToTable("AircraftTypes", "seat");

            entity.HasKey(a => a.AircraftTypeCode);

            entity.Property(a => a.AircraftTypeCode)
                  .HasColumnName("AircraftTypeCode")
                  .HasColumnType("nvarchar(4)")
                  .IsRequired();

            entity.Property(a => a.Manufacturer)
                  .HasColumnName("Manufacturer")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(a => a.FriendlyName)
                  .HasColumnName("FriendlyName")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired(false);

            entity.Property(a => a.TotalSeats)
                  .HasColumnName("TotalSeats")
                  .HasColumnType("int");

            entity.Property(a => a.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit");

            entity.Property(a => a.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset");

            entity.Property(a => a.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset");
        });

        modelBuilder.Entity<Seatmap>(entity =>
        {
            entity.ToTable("Seatmaps", "seat");

            entity.HasKey(s => s.SeatmapId);

            entity.Property(s => s.SeatmapId)
                  .HasColumnName("SeatmapId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(s => s.AircraftTypeCode)
                  .HasColumnName("AircraftTypeCode")
                  .HasColumnType("nvarchar(4)")
                  .IsRequired();

            entity.Property(s => s.Version)
                  .HasColumnName("Version")
                  .HasColumnType("int");

            entity.Property(s => s.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit");

            entity.Property(s => s.CabinLayout)
                  .HasColumnName("CabinLayout")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(s => s.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset");

            entity.Property(s => s.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset");
        });

        modelBuilder.Entity<SeatPricing>(entity =>
        {
            entity.ToTable("SeatPricings", "seat");

            entity.HasKey(p => p.SeatPricingId);

            entity.Property(p => p.SeatPricingId)
                  .HasColumnName("SeatPricingId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(p => p.CabinCode)
                  .HasColumnName("CabinCode")
                  .HasColumnType("nvarchar(10)")
                  .IsRequired();

            entity.Property(p => p.SeatPosition)
                  .HasColumnName("SeatPosition")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.Property(p => p.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("nvarchar(3)")
                  .IsRequired();

            entity.Property(p => p.Price)
                  .HasColumnName("Price")
                  .HasColumnType("decimal(18,2)");

            entity.Property(p => p.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit");

            entity.Property(p => p.ValidFrom)
                  .HasColumnName("ValidFrom")
                  .HasColumnType("datetimeoffset");

            entity.Property(p => p.ValidTo)
                  .HasColumnName("ValidTo")
                  .HasColumnType("datetimeoffset")
                  .IsRequired(false);

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset");

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset");
        });
    }
}
