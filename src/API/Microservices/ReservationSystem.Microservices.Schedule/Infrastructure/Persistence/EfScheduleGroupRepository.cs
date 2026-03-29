using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Infrastructure.Persistence;

public sealed class EfScheduleGroupRepository : IScheduleGroupRepository
{
    private readonly ScheduleDbContext _context;

    public EfScheduleGroupRepository(ScheduleDbContext context) => _context = context;

    public async Task<IReadOnlyList<ScheduleGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScheduleGroups
            .OrderByDescending(g => g.IsActive)
            .ThenByDescending(g => g.SeasonStart)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduleGroup?> GetByIdAsync(Guid scheduleGroupId, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduleGroups
            .FirstOrDefaultAsync(g => g.ScheduleGroupId == scheduleGroupId, cancellationToken);
    }

    public async Task AddAsync(ScheduleGroup group, CancellationToken cancellationToken = default)
    {
        _context.ScheduleGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ScheduleGroup group, CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid scheduleGroupId, CancellationToken cancellationToken = default)
    {
        await _context.FlightSchedules
            .Where(s => s.ScheduleGroupId == scheduleGroupId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.ScheduleGroups
            .Where(g => g.ScheduleGroupId == scheduleGroupId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
