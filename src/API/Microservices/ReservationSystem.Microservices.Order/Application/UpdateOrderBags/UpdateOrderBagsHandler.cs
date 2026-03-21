using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderBags;

/// <summary>
/// Handles the <see cref="UpdateOrderBagsCommand"/>.
/// Adds bag ancillaries to a confirmed order post-booking.
/// </summary>
public sealed class UpdateOrderBagsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<UpdateOrderBagsHandler> _logger;

    public UpdateOrderBagsHandler(
        IOrderRepository repository,
        ILogger<UpdateOrderBagsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        UpdateOrderBagsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
