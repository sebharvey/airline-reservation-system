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

    public async Task<FlightSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FlightSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ScheduleId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FlightSchedule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _context.FlightSchedules
            .AsNoTracking()
            .OrderBy(s => s.FlightNumber)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);

        return schedules.AsReadOnly();
    }

    public async Task CreateAsync(FlightSchedule schedule, CancellationToken cancellationToken = default)
    {
        _context.FlightSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted FlightSchedule {ScheduleId} into [schedule].[FlightSchedule]", schedule.ScheduleId);
    }

    public async Task UpdateAsync(FlightSchedule schedule, CancellationToken cancellationToken = default)
    {
        _context.FlightSchedules.Update(schedule);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for FlightSchedule {ScheduleId}", schedule.ScheduleId);
        else
            _logger.LogDebug("Updated FlightSchedule {ScheduleId} in [schedule].[FlightSchedule]", schedule.ScheduleId);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _context.FlightSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == id, cancellationToken);

        if (schedule is null)
            return;

        _context.FlightSchedules.Remove(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted FlightSchedule {ScheduleId} from [schedule].[FlightSchedule]", id);
    }
}
