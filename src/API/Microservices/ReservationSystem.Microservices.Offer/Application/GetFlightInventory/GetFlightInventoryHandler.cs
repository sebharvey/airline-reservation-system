using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventory;

public sealed class GetFlightInventoryHandler
{
    private readonly IOfferRepository _repository;

    public GetFlightInventoryHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FlightInventory>> HandleAsync(
        GetFlightInventoryQuery query,
        CancellationToken ct = default)
    {
        return await _repository.GetInventoriesByFlightAsync(query.FlightNumber, query.DepartureDate, ct);
    }
}
