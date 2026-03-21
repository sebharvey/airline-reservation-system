using ReservationSystem.Microservices.Seat.Domain.Entities;

namespace ReservationSystem.Microservices.Seat.Domain.Repositories;

public interface ISeatPricingRepository
{
    Task<SeatPricing?> GetByIdAsync(Guid seatPricingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeatPricing>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeatPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<SeatPricing> CreateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default);
    Task<SeatPricing?> UpdateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid seatPricingId, CancellationToken cancellationToken = default);
}
