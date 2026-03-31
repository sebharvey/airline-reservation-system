using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightByInventoryId;

public sealed class GetFlightByInventoryIdHandler
{
    private readonly IOfferRepository _repository;

    public GetFlightByInventoryIdHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<FlightInventory?> HandleAsync(
        GetFlightByInventoryIdQuery query,
        CancellationToken ct = default)
    {
        return await _repository.GetInventoryByIdAsync(query.InventoryId, ct);
    }
}
