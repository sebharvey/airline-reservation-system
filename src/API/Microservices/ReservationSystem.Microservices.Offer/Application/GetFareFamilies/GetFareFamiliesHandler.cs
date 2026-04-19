using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFareFamilies;

public sealed class GetFareFamiliesHandler
{
    private readonly IOfferRepository _repository;

    public GetFareFamiliesHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FareFamily>> HandleAsync(GetFareFamiliesQuery query, CancellationToken ct = default)
    {
        return await _repository.GetAllFareFamiliesAsync(ct);
    }
}
