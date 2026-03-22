using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.UpdateCustomer;

/// <summary>
/// Handles the <see cref="UpdateCustomerCommand"/>.
/// </summary>
public sealed class UpdateCustomerHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<UpdateCustomerHandler> _logger;

    public UpdateCustomerHandler(
        ICustomerRepository repository,
        ILogger<UpdateCustomerHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Customer?> HandleAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return null;
        }

        customer.UpdateProfile(
            givenName: command.GivenName,
            surname: command.Surname,
            dateOfBirth: command.DateOfBirth,
            nationality: command.Nationality,
            preferredLanguage: command.PreferredLanguage,
            phoneNumber: command.PhoneNumber,
            tierCode: command.TierCode,
            identityId: command.IdentityId,
            isActive: command.IsActive);

        await _repository.UpdateAsync(customer, cancellationToken);

        _logger.LogInformation("Updated customer {LoyaltyNumber}", command.LoyaltyNumber);

        return customer;
    }
}
