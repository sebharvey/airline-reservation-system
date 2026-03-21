using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetStoredOffer;

public sealed class GetStoredOfferHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetStoredOfferHandler> _logger;

    public GetStoredOfferHandler(IOfferRepository repository, ILogger<GetStoredOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<StoredOffer?> HandleAsync(GetStoredOfferQuery query, CancellationToken ct = default)
    {
        var offer = await _repository.GetStoredOfferAsync(query.OfferId, ct);

        if (offer is null)
        {
            _logger.LogWarning("StoredOffer {OfferId} not found", query.OfferId);
            return null;
        }

        if (offer.IsConsumed || offer.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("StoredOffer {OfferId} is expired or consumed", query.OfferId);
            return null;
        }

        return offer;
    }
}
