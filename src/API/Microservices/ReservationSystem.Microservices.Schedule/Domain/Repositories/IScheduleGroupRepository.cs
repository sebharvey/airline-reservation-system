namespace ReservationSystem.Microservices.Schedule.Domain.Repositories;

public interface IScheduleGroupRepository
{
    Task<IReadOnlyList<Entities.ScheduleGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Entities.ScheduleGroup?> GetByIdAsync(Guid scheduleGroupId, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.ScheduleGroup group, CancellationToken cancellationToken = default);
    Task UpdateAsync(Entities.ScheduleGroup group, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid scheduleGroupId, CancellationToken cancellationToken = default);
}
