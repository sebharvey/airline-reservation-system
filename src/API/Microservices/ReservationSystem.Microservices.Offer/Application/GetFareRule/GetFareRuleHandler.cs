using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFareRule;

public sealed class GetFareRuleHandler
{
    private readonly IOfferRepository _repository;

    public GetFareRuleHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<FareRule?> HandleAsync(GetFareRuleQuery query, CancellationToken ct = default)
    {
        return await _repository.GetFareRuleByIdAsync(query.FareRuleId, ct);
    }
}
