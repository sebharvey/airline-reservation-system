using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPricing;

public sealed class GetBagPricingHandler
{
    private readonly IBagPricingRepository _repository;

    public GetBagPricingHandler(IBagPricingRepository repository)
    {
        _repository = repository;
    }

    public async Task<BagPricing?> HandleAsync(GetBagPricingQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.PricingId, cancellationToken);
    }
}
