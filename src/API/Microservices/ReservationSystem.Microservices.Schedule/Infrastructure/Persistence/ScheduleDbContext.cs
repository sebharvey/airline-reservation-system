using Microsoft.EntityFrameworkCore;

namespace ReservationSystem.Microservices.Schedule.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Schedule microservice.
/// Manages FlightSchedule entities under the [schedule] schema.
/// </summary>
public sealed class ScheduleDbContext : DbContext
{
    public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.FlightSchedule> FlightSchedules => Set<Domain.Entities.FlightSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("schedule");

        modelBuilder.Entity<Domain.Entities.FlightSchedule>(entity =>
        {
            entity.ToTable("FlightSchedule");
            entity.HasKey(e => e.ScheduleId);
            entity.Property(e => e.ScheduleId).ValueGeneratedNever();
            entity.Property(e => e.FlightNumber).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Origin).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ValidFrom).IsRequired();
            entity.Property(e => e.ValidTo).IsRequired();
            entity.Property(e => e.FlightsCreatedCount).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }
}
