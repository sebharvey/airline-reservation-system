using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetOrderDebug;

/// <summary>
/// Handles the <see cref="GetOrderDebugQuery"/>.
/// Retrieves a raw order row by booking reference — for debug use only.
/// </summary>
public sealed class GetOrderDebugHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetOrderDebugHandler> _logger;

    public GetOrderDebugHandler(
        IOrderRepository repository,
        ILogger<GetOrderDebugHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        GetOrderDebugQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Debug: retrieving order by booking reference {BookingReference}", query.BookingReference);

        return await _repository.GetByBookingReferenceAsync(query.BookingReference, cancellationToken);
    }
}
