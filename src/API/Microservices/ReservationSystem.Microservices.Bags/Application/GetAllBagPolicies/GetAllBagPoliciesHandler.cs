using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetAllBagPolicies;

public sealed class GetAllBagPoliciesHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<GetAllBagPoliciesHandler> _logger;

    public GetAllBagPoliciesHandler(IBagPolicyRepository repository, ILogger<GetAllBagPoliciesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.BagPolicy>> HandleAsync(
        GetAllBagPoliciesQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
