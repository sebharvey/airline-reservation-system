using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;

/// <summary>
/// Handles the <see cref="UpdateBasketFlightsCommand"/>.
/// Adds or replaces flight selections within an existing basket.
/// </summary>
public sealed class UpdateBasketFlightsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketFlightsHandler> _logger;

    public UpdateBasketFlightsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketFlightsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketFlightsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
