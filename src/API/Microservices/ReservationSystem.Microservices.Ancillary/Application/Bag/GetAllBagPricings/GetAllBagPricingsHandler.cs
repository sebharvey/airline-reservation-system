using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPricings;

public sealed class GetAllBagPricingsHandler
{
    private readonly IBagPricingRepository _repository;

    public GetAllBagPricingsHandler(IBagPricingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<BagPricing>> HandleAsync(GetAllBagPricingsQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
