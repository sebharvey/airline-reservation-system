using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public async Task<Customer?> HandleAsync(
        GetCustomerQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
