using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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
        throw new NotImplementedException();
    }
}
