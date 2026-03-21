using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetBagPricing;

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
