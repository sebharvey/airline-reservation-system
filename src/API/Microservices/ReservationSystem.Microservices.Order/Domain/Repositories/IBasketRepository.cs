using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Domain.Repositories;

/// <summary>
/// Port (interface) for Basket persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IBasketRepository
{
    Task<Basket?> GetByIdAsync(Guid basketId, CancellationToken cancellationToken = default);

    Task CreateAsync(Basket basket, CancellationToken cancellationToken = default);

    Task UpdateAsync(Basket basket, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid basketId, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
