using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SearchFareRules;

public sealed class SearchFareRulesHandler
{
    private readonly IOfferRepository _repository;

    public SearchFareRulesHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FareRule>> HandleAsync(SearchFareRulesQuery query, CancellationToken ct = default)
    {
        return await _repository.SearchFareRulesAsync(query.Query, ct);
    }
}
