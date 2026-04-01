using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPolicies;

public sealed class GetAllBagPoliciesHandler
{
    private readonly IBagPolicyRepository _repository;

    public GetAllBagPoliciesHandler(IBagPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<BagPolicy>> HandleAsync(GetAllBagPoliciesQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
