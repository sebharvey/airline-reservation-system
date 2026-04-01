using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

public interface IAircraftTypeRepository
{
    Task<AircraftType?> GetByCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AircraftType>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AircraftType> CreateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default);
    Task<AircraftType?> UpdateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);
}
