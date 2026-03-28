namespace ReservationSystem.Microservices.Schedule.Domain.Repositories;

/// <summary>
/// Port (interface) for FlightSchedule persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IFlightScheduleRepository
{
    /// <summary>
    /// Deletes all existing FlightSchedule records and inserts the supplied set as a full replacement.
    /// Returns the count of records that were deleted.
    /// </summary>
    Task<int> ReplaceAllAsync(
        IReadOnlyList<Entities.FlightSchedule> schedules,
        CancellationToken cancellationToken = default);
}
