using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPolicy;

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
