using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateBasket;

/// <summary>
/// Handles the <see cref="CreateBasketCommand"/>.
/// Creates and persists a new <see cref="Basket"/>.
/// </summary>
public sealed class CreateBasketHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<CreateBasketHandler> _logger;

    public CreateBasketHandler(
        IBasketRepository repository,
        ILogger<CreateBasketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket> HandleAsync(
        CreateBasketCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
