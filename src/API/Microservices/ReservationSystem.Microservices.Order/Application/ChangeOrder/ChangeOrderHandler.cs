using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.ChangeOrder;

/// <summary>
/// Handles the <see cref="ChangeOrderCommand"/>.
/// Changes flight details on a confirmed order (voluntary change).
/// </summary>
public sealed class ChangeOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<ChangeOrderHandler> _logger;

    public ChangeOrderHandler(
        IOrderRepository repository,
        ILogger<ChangeOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        ChangeOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
