using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Domain.Repositories;

/// <summary>
/// Port (interface) for Order persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IOrderRepository
{
    Task<Entities.Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Entities.Order?> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.Order order, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.Order order, CancellationToken cancellationToken = default);
}
