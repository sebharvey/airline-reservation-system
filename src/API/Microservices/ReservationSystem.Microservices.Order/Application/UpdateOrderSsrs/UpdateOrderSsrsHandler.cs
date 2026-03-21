using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;

/// <summary>
/// Handles the <see cref="UpdateOrderSsrsCommand"/>.
/// Updates Special Service Requests on a confirmed order.
/// </summary>
public sealed class UpdateOrderSsrsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderSsrsHandler> _logger;

    public UpdateOrderSsrsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderSsrsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderSsrsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
