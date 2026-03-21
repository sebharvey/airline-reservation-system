using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Domain.Repositories;

/// <summary>
/// Port (interface) for Seatmap persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ISeatmapRepository
{
    Task<Seatmap?> GetByIdAsync(Guid seatmapId, CancellationToken cancellationToken = default);

    Task<Seatmap?> GetActiveByAircraftTypeCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default);

    Task CreateAsync(Seatmap seatmap, CancellationToken cancellationToken = default);

    Task UpdateAsync(Seatmap seatmap, CancellationToken cancellationToken = default);
}
