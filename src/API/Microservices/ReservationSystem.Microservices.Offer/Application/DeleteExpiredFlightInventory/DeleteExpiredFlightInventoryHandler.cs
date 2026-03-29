using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.DeleteExpiredFlightInventory;

/// <summary>
/// Deletes all FlightInventory rows (and their child Fare rows) whose departure
/// datetime is more than 48 hours in the past. Intended to be invoked by a
/// scheduled timer trigger.
/// </summary>
public sealed class DeleteExpiredFlightInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<DeleteExpiredFlightInventoryHandler> _logger;

    public DeleteExpiredFlightInventoryHandler(
        IOfferRepository repository,
        ILogger<DeleteExpiredFlightInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired flight inventory cleanup");

        var deletedCount = await _repository.DeleteExpiredFlightInventoryAsync(cancellationToken);

        _logger.LogInformation("Expired flight inventory cleanup complete. Deleted {Count} inventory row(s)", deletedCount);

        return deletedCount;
    }
}
