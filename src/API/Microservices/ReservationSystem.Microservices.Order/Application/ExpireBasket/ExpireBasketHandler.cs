using Microsoft.Extensions.Logging;
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
        throw new NotImplementedException();
    }
}
