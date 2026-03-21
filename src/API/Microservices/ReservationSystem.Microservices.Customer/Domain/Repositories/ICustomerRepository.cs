using ReservationSystem.Microservices.Customer.Domain.Entities;

namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

/// <summary>
/// Port (interface) for Customer persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default);

    Task CreateAsync(Customer customer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);

    Task DeleteAsync(string loyaltyNumber, CancellationToken cancellationToken = default);
}
