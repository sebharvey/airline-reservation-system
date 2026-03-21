using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Domain.Repositories;

/// <summary>
/// Port (interface) for AircraftType persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IAircraftTypeRepository
{
    Task<AircraftType?> GetByCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AircraftType>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default);

    Task UpdateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default);
}
