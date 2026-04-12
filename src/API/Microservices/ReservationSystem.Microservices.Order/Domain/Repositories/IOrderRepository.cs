using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Domain.Repositories;

public interface IOrderRepository
{
    Task<Entities.Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string?>> GetBookingReferencesByIdsAsync(IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken = default);

    Task<Entities.Order?> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.Order>> GetByFlightAsync(string flightNumber, string departureDate, string? status = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.Order>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.Order order, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.Order order, CancellationToken cancellationToken = default);

    Task<bool> DeleteDraftOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredDraftOrdersAsync(CancellationToken cancellationToken = default);
}
