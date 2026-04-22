using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightAvailability;

public sealed class GetFlightAvailabilityHandler
{
    private readonly IOfferRepository _repository;

    public GetFlightAvailabilityHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FlightInventory>> HandleAsync(
        GetFlightAvailabilityQuery query,
        CancellationToken ct = default)
    {
        return await _repository.SearchAvailableInventoryByRangeAsync(
            query.Origin, query.Destination, query.FromDate, query.ToDate, ct);
    }
}
