using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPolicy;

public sealed class CreateBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<CreateBagPolicyHandler> _logger;

    public CreateBagPolicyHandler(IBagPolicyRepository repository, ILogger<CreateBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BagPolicy> HandleAsync(CreateBagPolicyCommand command, CancellationToken cancellationToken = default)
    {
        var policy = BagPolicy.Create(command.CabinCode, command.FreeBagsIncluded, command.MaxWeightKgPerBag);
        var created = await _repository.CreateAsync(policy, cancellationToken);
        _logger.LogInformation("Created BagPolicy {PolicyId} for cabin {CabinCode}", created.PolicyId, command.CabinCode);
        return created;
    }
}
