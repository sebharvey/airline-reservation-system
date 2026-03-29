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
    public async Task<IReadOnlyList<FlightSchedule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FlightSchedules
            .OrderBy(s => s.FlightNumber)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FlightSchedule>> GetByGroupAsync(
        Guid scheduleGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FlightSchedules
            .Where(s => s.ScheduleGroupId == scheduleGroupId)
            .OrderBy(s => s.FlightNumber)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ReplaceByGroupAsync(
        Guid scheduleGroupId,
        IReadOnlyList<FlightSchedule> schedules,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _context.FlightSchedules
            .Where(s => s.ScheduleGroupId == scheduleGroupId)
            .ExecuteDeleteAsync(cancellationToken);

        _context.FlightSchedules.AddRange(schedules);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "ReplaceByGroupAsync: group={GroupId}, deleted {Deleted} existing records, inserted {Inserted} new records",
            scheduleGroupId, deleted, schedules.Count);

        return deleted;
    }
}
