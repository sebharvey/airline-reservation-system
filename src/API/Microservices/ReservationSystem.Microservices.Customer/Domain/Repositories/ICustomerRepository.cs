namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

/// <summary>
/// Port (interface) for Customer persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ICustomerRepository
{
    Task<Domain.Entities.Customer?> GetByLoyaltyNumberAsync(string loyaltyNumber, CancellationToken cancellationToken = default);

    Task<Domain.Entities.Customer?> GetByIdentityIdAsync(Guid identityId, CancellationToken cancellationToken = default);

    Task CreateAsync(Domain.Entities.Customer customer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Domain.Entities.Customer customer, CancellationToken cancellationToken = default);

    Task DeleteAsync(string loyaltyNumber, CancellationToken cancellationToken = default);
}
