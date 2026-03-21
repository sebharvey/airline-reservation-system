using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetBasket;

/// <summary>
/// Handles the <see cref="GetBasketQuery"/>.
/// Retrieves a basket by its identifier.
/// </summary>
public sealed class GetBasketHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<GetBasketHandler> _logger;

    public GetBasketHandler(
        IBasketRepository repository,
        ILogger<GetBasketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        GetBasketQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
