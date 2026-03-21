using ReservationSystem.Microservices.Customer.Domain.Entities;

namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

/// <summary>
/// Port (interface) for LoyaltyTransaction persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ILoyaltyTransactionRepository
{
    Task<IReadOnlyList<LoyaltyTransaction>> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default);

    Task CreateAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default);
}
