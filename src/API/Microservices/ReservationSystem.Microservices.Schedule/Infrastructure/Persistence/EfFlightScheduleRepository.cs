using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IFlightScheduleRepository"/>.
/// Uses <see cref="ScheduleDbContext"/> to interact with the [schedule].[FlightSchedule] table.
/// </summary>
public sealed class EfFlightScheduleRepository : IFlightScheduleRepository
{
    private readonly ScheduleDbContext _context;
    private readonly ILogger<EfFlightScheduleRepository> _logger;

    public EfFlightScheduleRepository(ScheduleDbContext context, ILogger<EfFlightScheduleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ReplaceAllAsync(
        IReadOnlyList<FlightSchedule> schedules,
        CancellationToken cancellationToken = default)
    {
        // Count existing records before deletion for the return value.
        var deleted = await _context.FlightSchedules.CountAsync(cancellationToken);

        // Remove all existing records.
        await _context.FlightSchedules.ExecuteDeleteAsync(cancellationToken);

        // Insert all incoming records.
        _context.FlightSchedules.AddRange(schedules);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "ReplaceAllAsync: deleted {Deleted} existing records, inserted {Inserted} new records",
            deleted, schedules.Count);

        return deleted;
    }
}
