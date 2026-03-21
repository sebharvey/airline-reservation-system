using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.ExpireBasket;

/// <summary>
/// Handles the <see cref="ExpireBasketCommand"/>.
/// Marks an open basket as expired.
/// </summary>
public sealed class ExpireBasketHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<ExpireBasketHandler> _logger;

    public ExpireBasketHandler(
        IBasketRepository repository,
        ILogger<ExpireBasketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        ExpireBasketCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Expiring basket {BasketId}", command.BasketId);

        var basket = await _repository.GetByIdAsync(command.BasketId, cancellationToken);
        if (basket is null)
        {
            _logger.LogWarning("Basket {BasketId} not found", command.BasketId);
            return false;
        }

        if (basket.BasketStatus != BasketStatusValues.Active)
        {
            _logger.LogWarning("Basket {BasketId} is not open (status: {Status}), cannot expire",
                command.BasketId, basket.BasketStatus);
            return false;
        }

        basket.Expire();

        await _repository.UpdateAsync(basket, cancellationToken);

        _logger.LogInformation("Basket {BasketId} expired", command.BasketId);

        return true;
    }
}
