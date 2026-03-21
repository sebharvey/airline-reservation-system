using Microsoft.Extensions.Logging;
using Customer = global::ReservationSystem.Microservices.Customer.Domain.Entities.Customer;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.CreateCustomer;

/// <summary>
/// Handles the <see cref="CreateCustomerCommand"/>.
/// </summary>
public sealed class CreateCustomerHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<CreateCustomerHandler> _logger;

    public CreateCustomerHandler(
        ICustomerRepository repository,
        ILogger<CreateCustomerHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Customer> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var customer = Customer.Create(
            loyaltyNumber: command.LoyaltyNumber,
            givenName: command.GivenName,
            surname: command.Surname,
            preferredLanguage: command.PreferredLanguage,
            tierCode: "Blue",
            identityReference: command.IdentityReference,
            dateOfBirth: command.DateOfBirth);

        await _repository.CreateAsync(customer, cancellationToken);

        _logger.LogInformation("Created customer {LoyaltyNumber}", customer.LoyaltyNumber);

        return customer;
    }
}
