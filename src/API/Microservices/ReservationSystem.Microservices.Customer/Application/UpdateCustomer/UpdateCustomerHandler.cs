using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public async Task<Customer?> HandleAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
