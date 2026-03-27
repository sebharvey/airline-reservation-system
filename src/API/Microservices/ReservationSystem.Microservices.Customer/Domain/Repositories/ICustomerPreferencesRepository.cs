namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

/// <summary>
/// Port (interface) for CustomerPreferences persistence.
/// </summary>
public interface ICustomerPreferencesRepository
{
    Task<Domain.Entities.CustomerPreferences?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<Domain.Entities.CustomerPreferences> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Domain.Entities.CustomerPreferences preferences, CancellationToken cancellationToken = default);

    Task DeleteByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}
