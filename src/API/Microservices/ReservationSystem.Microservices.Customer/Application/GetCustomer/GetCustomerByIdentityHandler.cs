using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetCustomer;

/// <summary>
/// Handles the <see cref="GetCustomerByIdentityQuery"/>.
/// </summary>
public sealed class GetCustomerByIdentityHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<GetCustomerByIdentityHandler> _logger;

    public GetCustomerByIdentityHandler(
        ICustomerRepository repository,
        ILogger<GetCustomerByIdentityHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Customer?> HandleAsync(
        GetCustomerByIdentityQuery query,
        CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdentityIdAsync(query.IdentityId, cancellationToken);

        if (customer is null)
            _logger.LogDebug("Customer not found for IdentityId {IdentityId}", query.IdentityId);

        return customer;
    }
}
