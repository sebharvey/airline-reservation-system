using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.DeleteCustomer;

/// <summary>
/// Handles the <see cref="DeleteCustomerCommand"/>.
/// </summary>
public sealed class DeleteCustomerHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<DeleteCustomerHandler> _logger;

    public DeleteCustomerHandler(
        ICustomerRepository repository,
        ILogger<DeleteCustomerHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DeleteCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);

        if (customer is null)
        {
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", command.LoyaltyNumber);
            return false;
        }

        await _repository.DeleteAsync(command.LoyaltyNumber, cancellationToken);

        _logger.LogInformation("Deleted customer {LoyaltyNumber}", command.LoyaltyNumber);

        return true;
    }
}
