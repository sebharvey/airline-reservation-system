using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CancelOrder;

/// <summary>
/// Handles the <see cref="CancelOrderCommand"/>.
/// Cancels a confirmed order.
/// </summary>
public sealed class CancelOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CancelOrderHandler> _logger;

    public CancelOrderHandler(
        IOrderRepository repository,
        ILogger<CancelOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
