using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.DeleteExpiredBaskets;

/// <summary>
/// Deletes all baskets whose <c>ExpiresAt</c> timestamp is in the past.
/// Intended to be invoked by a scheduled timer trigger.
/// </summary>
public sealed class DeleteExpiredBasketsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<DeleteExpiredBasketsHandler> _logger;

    public DeleteExpiredBasketsHandler(
        IBasketRepository repository,
        ILogger<DeleteExpiredBasketsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired basket cleanup");

        var deletedCount = await _repository.DeleteExpiredAsync(cancellationToken);

        _logger.LogInformation("Expired basket cleanup complete. Deleted {Count} basket(s)", deletedCount);

        return deletedCount;
    }
}
