using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetInventoryHolds;

public sealed class GetInventoryHoldsHandler
{
    private readonly IOfferRepository _repository;

    public GetInventoryHoldsHandler(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<InventoryHoldRecord>> HandleAsync(
        GetInventoryHoldsQuery query,
        CancellationToken ct = default)
    {
        return await _repository.GetHoldsByInventoryAsync(query.InventoryId, ct);
    }
}
