using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Domain.Repositories;

/// <summary>
/// Port (interface) for SeatPricing persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ISeatPricingRepository
{
    Task<SeatPricing?> GetByIdAsync(Guid seatPricingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeatPricing>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default);

    Task UpdateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid seatPricingId, CancellationToken cancellationToken = default);
}
