using Microsoft.Extensions.Logging;
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

    public async Task<Domain.Entities.Customer> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var loyaltyNumber = await GenerateUniqueLoyaltyNumberAsync(cancellationToken);

        var customer = Domain.Entities.Customer.Create(
            loyaltyNumber: loyaltyNumber,
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

    private async Task<string> GenerateUniqueLoyaltyNumberAsync(CancellationToken cancellationToken)
    {
        string loyaltyNumber;

        do
        {
            var random = Random.Shared.Next(1000000, 9999999);
            loyaltyNumber = $"AX{random}";
        }
        while (await _repository.GetByLoyaltyNumberAsync(loyaltyNumber, cancellationToken) is not null);

        return loyaltyNumber;
    }
}
