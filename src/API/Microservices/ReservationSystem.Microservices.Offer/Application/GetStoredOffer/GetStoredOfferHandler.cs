using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetStoredOffer;

/// <summary>
/// Pairs the stored offer with its FlightInventory so callers have full flight
/// context without re-reading it separately. Flight details are not persisted in
/// FaresInfo (kept lean) and are resolved here at read time.
/// </summary>
public sealed record GetStoredOfferResult(StoredOffer Offer, FlightInventory Inventory);

public sealed class GetStoredOfferHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetStoredOfferHandler> _logger;

    public GetStoredOfferHandler(IOfferRepository repository, ILogger<GetStoredOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<GetStoredOfferResult?> HandleAsync(GetStoredOfferQuery query, CancellationToken ct = default)
    {
        var offer = await _repository.GetStoredOfferByOfferIdAsync(query.OfferId, ct);

        if (offer is null)
        {
            _logger.LogWarning("Offer {OfferId} not found", query.OfferId);
            return null;
        }

        if (offer.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Offer {OfferId} is expired", query.OfferId);
            throw new OfferGoneException($"Offer {query.OfferId} has expired. Customer must re-search.");
        }

        var inventoryId = offer.GetFaresInfo().InventoryId;
        var inventory   = await _repository.GetInventoryByIdAsync(inventoryId, ct);

        if (inventory is null)
        {
            _logger.LogWarning("FlightInventory {InventoryId} for offer {OfferId} not found", inventoryId, query.OfferId);
            return null;
        }

        return new GetStoredOfferResult(offer, inventory);
    }
}
