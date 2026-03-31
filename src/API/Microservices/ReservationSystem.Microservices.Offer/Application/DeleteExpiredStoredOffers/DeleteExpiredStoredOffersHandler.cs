using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.DeleteExpiredStoredOffers;

/// <summary>
/// Deletes all StoredOffer rows whose ExpiresAt is in the past.
/// Intended to be invoked by a scheduled timer trigger.
/// </summary>
public sealed class DeleteExpiredStoredOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<DeleteExpiredStoredOffersHandler> _logger;

    public DeleteExpiredStoredOffersHandler(
        IOfferRepository repository,
        ILogger<DeleteExpiredStoredOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired stored offer cleanup");

        var deletedCount = await _repository.DeleteExpiredStoredOffersAsync(cancellationToken);

        _logger.LogInformation("Expired stored offer cleanup complete. Deleted {Count} stored offer row(s)", deletedCount);

        return deletedCount;
    }
}
