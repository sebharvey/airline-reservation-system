namespace ReservationSystem.Microservices.Schedule.Domain.Repositories;

/// <summary>
/// Port (interface) for FlightSchedule persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IFlightScheduleRepository
{
    /// <summary>
    /// Deletes all FlightSchedule records within the given schedule group and inserts the supplied set.
    /// Returns the count of records that were deleted.
    /// </summary>
    Task<int> ReplaceByGroupAsync(
        Guid scheduleGroupId,
        IReadOnlyList<Entities.FlightSchedule> schedules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all FlightSchedule records ordered by FlightNumber then ValidFrom.
    /// </summary>
    Task<IReadOnlyList<Entities.FlightSchedule>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns FlightSchedule records for a specific schedule group.
    /// </summary>
    Task<IReadOnlyList<Entities.FlightSchedule>> GetByGroupAsync(
        Guid scheduleGroupId,
        CancellationToken cancellationToken = default);
}
