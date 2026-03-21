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
        _logger.LogInformation("Retrieving order by booking reference {BookingReference}", query.BookingReference);

        var order = await _repository.GetByBookingReferenceAsync(query.BookingReference, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order with booking reference {BookingReference} not found", query.BookingReference);
        }

        return order;
    }
}
