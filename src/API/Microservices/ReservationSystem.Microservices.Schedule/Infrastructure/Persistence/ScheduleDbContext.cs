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
            entity.ToTable("FlightSchedule", "schedule", t =>
            {
                t.HasTrigger("TR_FlightSchedule_UpdatedAt");
                t.UseSqlOutputClause(false);
            });
            entity.HasKey(e => e.ScheduleId);

            entity.Property(e => e.ScheduleId)
                  .HasColumnName("ScheduleId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(e => e.FlightNumber)
                  .HasColumnName("FlightNumber")
                  .HasColumnType("varchar(10)")
                  .HasMaxLength(10)
                  .IsRequired();

            entity.Property(e => e.Origin)
                  .HasColumnName("Origin")
                  .HasColumnType("char(3)")
                  .HasMaxLength(3)
                  .IsRequired();

            entity.Property(e => e.Destination)
                  .HasColumnName("Destination")
                  .HasColumnType("char(3)")
                  .HasMaxLength(3)
                  .IsRequired();

            entity.Property(e => e.DepartureTime)
                  .HasColumnName("DepartureTime")
                  .HasColumnType("time")
                  .IsRequired();

            entity.Property(e => e.ArrivalTime)
                  .HasColumnName("ArrivalTime")
                  .HasColumnType("time")
                  .IsRequired();

            entity.Property(e => e.ArrivalDayOffset)
                  .HasColumnName("ArrivalDayOffset")
                  .HasColumnType("tinyint")
                  .IsRequired();

            entity.Property(e => e.DaysOfWeek)
                  .HasColumnName("DaysOfWeek")
                  .HasColumnType("tinyint")
                  .IsRequired();

            entity.Property(e => e.AircraftType)
                  .HasColumnName("AircraftType")
                  .HasColumnType("varchar(4)")
                  .HasMaxLength(4)
                  .IsRequired();

            entity.Property(e => e.ValidFrom)
                  .HasColumnName("ValidFrom")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(e => e.ValidTo)
                  .HasColumnName("ValidTo")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(e => e.FlightsCreated)
                  .HasColumnName("FlightsCreated")
                  .HasColumnType("int")
                  .IsRequired();

            entity.Property(e => e.CabinFares)
                  .HasColumnName("CabinFares")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasColumnName("CreatedBy")
                  .HasColumnType("varchar(100)")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.HasIndex(e => new { e.FlightNumber, e.ValidFrom, e.ValidTo })
                  .HasDatabaseName("IX_FlightSchedule_FlightNumber");
        });
    }
}
