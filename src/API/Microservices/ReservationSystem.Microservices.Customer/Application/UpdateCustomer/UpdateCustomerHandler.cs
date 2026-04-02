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

        if (command.IdentityId.HasValue)
        {
            var existing = await _repository.GetByIdentityIdAsync(command.IdentityId.Value, cancellationToken);
            if (existing is not null && existing.CustomerId != customer.CustomerId)
            {
                _logger.LogWarning(
                    "IdentityId {IdentityId} is already associated with customer {ExistingCustomerId}",
                    command.IdentityId.Value, existing.CustomerId);
                throw new InvalidOperationException(
                    $"IdentityId '{command.IdentityId.Value}' is already associated with another customer account.");
            }
        }

        customer.UpdateProfile(
            givenName: command.GivenName,
            surname: command.Surname,
            dateOfBirth: command.DateOfBirth,
            gender: command.Gender,
            nationality: command.Nationality,
            preferredLanguage: command.PreferredLanguage,
            phoneNumber: command.PhoneNumber,
            addressLine1: command.AddressLine1,
            addressLine2: command.AddressLine2,
            city: command.City,
            stateOrRegion: command.StateOrRegion,
            postalCode: command.PostalCode,
            countryCode: command.CountryCode,
            passportNumber: command.PassportNumber,
            passportIssueDate: command.PassportIssueDate,
            passportIssuer: command.PassportIssuer,
            passportExpiryDate: command.PassportExpiryDate,
            knownTravellerNumber: command.KnownTravellerNumber,
            tierCode: command.TierCode,
            identityId: command.IdentityId,
            isActive: command.IsActive);

        await _repository.UpdateAsync(customer, cancellationToken);

        _logger.LogInformation("Updated customer {LoyaltyNumber}", command.LoyaltyNumber);

        return customer;
    }
}
