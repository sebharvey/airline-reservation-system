using Microsoft.Extensions.Logging;
using Offer = ReservationSystem.Microservices.Offer.Domain.Entities.Offer;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UseCases.GetOffer;

/// <summary>
/// Handles the <see cref="GetOfferQuery"/>.
/// Orchestrates domain and repository interactions; contains no SQL or HTTP concerns.
/// </summary>
public sealed class GetOfferHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetOfferHandler> _logger;

    public GetOfferHandler(
        IOfferRepository repository,
        ILogger<GetOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Offer?> HandleAsync(
        GetOfferQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching Offer {Id}", query.Id);

        var offer = await _repository.GetByIdAsync(query.Id, cancellationToken);

        if (offer is null)
            _logger.LogWarning("Offer {Id} not found", query.Id);

        return offer;
    }
}
