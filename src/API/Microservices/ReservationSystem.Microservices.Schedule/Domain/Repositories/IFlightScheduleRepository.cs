namespace ReservationSystem.Microservices.Schedule.Domain.Repositories;

/// <summary>
/// Port (interface) for FlightSchedule persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IFlightScheduleRepository
{
    Task<Entities.FlightSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.FlightSchedule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.FlightSchedule schedule, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.FlightSchedule schedule, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
