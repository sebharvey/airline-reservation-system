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
        throw new NotImplementedException();
    }
}
