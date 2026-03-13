using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.OfferApi.Domain.Entities;
using ReservationSystem.Microservices.OfferApi.Domain.Repositories;

namespace ReservationSystem.Microservices.OfferApi.Application.UseCases.GetAllOffers;

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

    public async Task<IReadOnlyList<Offer>> HandleAsync(
        GetAllOffersQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all Offers");
        return await _repository.GetAllAsync(cancellationToken);
    }
}
