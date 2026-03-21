using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.RebookOrder;

/// <summary>
/// Handles the <see cref="RebookOrderCommand"/>.
/// Rebooks a passenger under IROPS onto an alternative flight.
/// </summary>
public sealed class RebookOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<RebookOrderHandler> _logger;

    public RebookOrderHandler(
        IOrderRepository repository,
        ILogger<RebookOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        RebookOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
