using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetPreferences;

public sealed class GetPreferencesHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPreferencesRepository _preferencesRepository;
    private readonly ILogger<GetPreferencesHandler> _logger;

    public GetPreferencesHandler(
        ICustomerRepository customerRepository,
        ICustomerPreferencesRepository preferencesRepository,
        ILogger<GetPreferencesHandler> logger)
    {
        _customerRepository = customerRepository;
        _preferencesRepository = preferencesRepository;
        _logger = logger;
    }

    /// <returns>Preferences (created with defaults if none exist), or null if the customer was not found.</returns>
    public async Task<CustomerPreferences?> HandleAsync(GetPreferencesQuery query, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(query.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", query.LoyaltyNumber);
            return null;
        }

        return await _preferencesRepository.GetOrCreateAsync(customer.CustomerId, cancellationToken);
    }
}
