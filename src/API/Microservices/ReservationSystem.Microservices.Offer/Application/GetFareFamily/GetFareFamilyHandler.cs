using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFareFamily;

public sealed class GetFareFamilyHandler
{
    private readonly IOfferRepository _repository;

    public GetFareFamilyHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<FareFamily?> HandleAsync(GetFareFamilyQuery query, CancellationToken ct = default)
    {
        return await _repository.GetFareFamilyByIdAsync(query.FareFamilyId, ct);
    }
}
