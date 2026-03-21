using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetOrder;

/// <summary>
/// Handles the <see cref="GetOrderQuery"/>.
/// Retrieves an order by its booking reference.
/// </summary>
public sealed class GetOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetOrderHandler> _logger;

    public GetOrderHandler(
        IOrderRepository repository,
        ILogger<GetOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
