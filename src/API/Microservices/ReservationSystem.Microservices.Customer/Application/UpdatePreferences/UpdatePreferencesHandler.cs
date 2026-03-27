using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.UpdatePreferences;

public sealed class UpdatePreferencesHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPreferencesRepository _preferencesRepository;
    private readonly ILogger<UpdatePreferencesHandler> _logger;

    public UpdatePreferencesHandler(
        ICustomerRepository customerRepository,
        ICustomerPreferencesRepository preferencesRepository,
        ILogger<UpdatePreferencesHandler> logger)
    {
        _customerRepository = customerRepository;
        _preferencesRepository = preferencesRepository;
        _logger = logger;
    }

    /// <returns>True if updated; false if the customer was not found.</returns>
    public async Task<bool> HandleAsync(UpdatePreferencesCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return false;
        }

        var preferences = await _preferencesRepository.GetOrCreateAsync(customer.CustomerId, cancellationToken);

        preferences.Update(
            marketingEnabled: command.MarketingEnabled,
            analyticsEnabled: command.AnalyticsEnabled,
            functionalEnabled: command.FunctionalEnabled,
            appNotificationsEnabled: command.AppNotificationsEnabled);

        await _preferencesRepository.UpdateAsync(preferences, cancellationToken);

        _logger.LogInformation("Updated Preferences for Customer {LoyaltyNumber}", command.LoyaltyNumber);
        return true;
    }
}
