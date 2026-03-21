using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;

public sealed class CreateBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<CreateBagPolicyHandler> _logger;

    public CreateBagPolicyHandler(IBagPolicyRepository repository, ILogger<CreateBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPolicy> HandleAsync(
        CreateBagPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
