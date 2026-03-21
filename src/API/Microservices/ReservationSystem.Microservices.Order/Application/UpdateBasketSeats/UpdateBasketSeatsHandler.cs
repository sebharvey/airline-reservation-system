using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;

/// <summary>
/// Handles the <see cref="UpdateBasketSeatsCommand"/>.
/// Adds or replaces seat selections within an existing basket.
/// </summary>
public sealed class UpdateBasketSeatsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketSeatsHandler> _logger;

    public UpdateBasketSeatsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketSeatsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketSeatsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
