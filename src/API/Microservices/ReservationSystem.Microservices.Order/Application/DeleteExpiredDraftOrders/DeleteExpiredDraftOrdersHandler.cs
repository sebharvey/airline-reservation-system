using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.DeleteExpiredDraftOrders;

/// <summary>
/// Deletes all Draft orders whose <c>UpdatedAt</c> timestamp is more than 24 hours in the past.
/// Intended to be invoked by a scheduled timer trigger.
/// </summary>
public sealed class DeleteExpiredDraftOrdersHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<DeleteExpiredDraftOrdersHandler> _logger;

    public DeleteExpiredDraftOrdersHandler(
        IOrderRepository repository,
        ILogger<DeleteExpiredDraftOrdersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired draft order cleanup");

        var deletedCount = await _repository.DeleteExpiredDraftOrdersAsync(cancellationToken);

        _logger.LogInformation("Expired draft order cleanup complete. Deleted {Count} draft order(s)", deletedCount);

        return deletedCount;
    }
}
