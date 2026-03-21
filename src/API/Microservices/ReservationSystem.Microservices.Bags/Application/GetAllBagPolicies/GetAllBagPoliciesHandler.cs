using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetAllBagPolicies;

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
