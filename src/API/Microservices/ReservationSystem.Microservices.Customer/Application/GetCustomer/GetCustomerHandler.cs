using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetCustomer;

/// <summary>
/// Handles the <see cref="GetCustomerQuery"/>.
/// </summary>
public sealed class GetCustomerHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<GetCustomerHandler> _logger;

    public GetCustomerHandler(
        ICustomerRepository repository,
        ILogger<GetCustomerHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Customer?> HandleAsync(
        GetCustomerQuery query,
        CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByLoyaltyNumberAsync(query.LoyaltyNumber, cancellationToken);

        if (customer is null)
            _logger.LogDebug("Customer not found for LoyaltyNumber {LoyaltyNumber}", query.LoyaltyNumber);

        return customer;
    }
}
