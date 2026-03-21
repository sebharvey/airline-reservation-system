using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;

/// <summary>
/// Handles the <see cref="UpdateOrderSeatsCommand"/>.
/// Updates seat assignments on a confirmed order post-booking.
/// </summary>
public sealed class UpdateOrderSeatsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderSeatsHandler> _logger;

    public UpdateOrderSeatsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderSeatsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderSeatsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
