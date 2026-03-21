using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetAllBagPricings;

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
