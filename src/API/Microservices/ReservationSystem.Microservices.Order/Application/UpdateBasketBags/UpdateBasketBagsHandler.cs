using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketBags;

/// <summary>
/// Handles the <see cref="UpdateBasketBagsCommand"/>.
/// Adds or replaces bag ancillary selections within an existing basket.
/// </summary>
public sealed class UpdateBasketBagsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketBagsHandler> _logger;

    public UpdateBasketBagsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketBagsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketBagsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
