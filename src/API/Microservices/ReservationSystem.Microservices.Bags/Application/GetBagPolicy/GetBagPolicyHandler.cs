using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetBagPolicy;

public sealed class GetBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<GetBagPolicyHandler> _logger;

    public GetBagPolicyHandler(IBagPolicyRepository repository, ILogger<GetBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPolicy?> HandleAsync(
        GetBagPolicyQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
