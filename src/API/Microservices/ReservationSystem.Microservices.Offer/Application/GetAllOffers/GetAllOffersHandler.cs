using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetAllOffers;

public sealed class GetAllOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetAllOffersHandler> _logger;

    public GetAllOffersHandler(
        IOfferRepository repository,
        ILogger<GetAllOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.Offer>> HandleAsync(
        GetAllOffersQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all Offers");
        return await _repository.GetAllAsync(cancellationToken);
    }
}
