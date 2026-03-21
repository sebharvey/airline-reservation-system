using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;

/// <summary>
/// Handles the <see cref="UpdateBasketPassengersCommand"/>.
/// Updates passenger details within an existing basket.
/// </summary>
public sealed class UpdateBasketPassengersHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketPassengersHandler> _logger;

    public UpdateBasketPassengersHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketPassengersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketPassengersCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
