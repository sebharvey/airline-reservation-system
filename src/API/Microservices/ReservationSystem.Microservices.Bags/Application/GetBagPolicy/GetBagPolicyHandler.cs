using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetBagPolicy;

public sealed class GetBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;

    public GetBagPolicyHandler(IBagPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<BagPolicy?> HandleAsync(GetBagPolicyQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.PolicyId, cancellationToken);
    }
}
