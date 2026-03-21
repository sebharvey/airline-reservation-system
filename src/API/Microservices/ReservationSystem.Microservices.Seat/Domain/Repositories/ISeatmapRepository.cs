using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Domain.Repositories;

public interface ISeatmapRepository
{
    Task<Seatmap?> GetByIdAsync(Guid seatmapId, CancellationToken cancellationToken = default);
    Task<Seatmap?> GetActiveByAircraftTypeCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Seatmap>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Seatmap> CreateAsync(Seatmap seatmap, CancellationToken cancellationToken = default);
    Task<Seatmap?> UpdateAsync(Seatmap seatmap, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid seatmapId, CancellationToken cancellationToken = default);
    Task DeactivateByAircraftTypeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSeatmapsAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);
}
