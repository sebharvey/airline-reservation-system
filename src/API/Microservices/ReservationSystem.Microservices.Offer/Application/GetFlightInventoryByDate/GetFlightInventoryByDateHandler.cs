using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;

public sealed class GetFlightInventoryByDateHandler
{
    private readonly IOfferRepository _repository;

    public GetFlightInventoryByDateHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FlightInventoryGroup>> HandleAsync(
        GetFlightInventoryByDateQuery query,
        CancellationToken ct = default)
    {
        return await _repository.GetInventoryGroupedByDateAsync(query.DepartureDate, ct);
    }
}
