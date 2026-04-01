using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPolicy;

public sealed class UpdateBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<UpdateBagPolicyHandler> _logger;

    public UpdateBagPolicyHandler(IBagPolicyRepository repository, ILogger<UpdateBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BagPolicy?> HandleAsync(UpdateBagPolicyCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.PolicyId, cancellationToken);
        if (existing is null) return null;

        var updated = BagPolicy.Reconstitute(
            command.PolicyId, existing.CabinCode, command.FreeBagsIncluded, command.MaxWeightKgPerBag,
            command.IsActive, existing.CreatedAt, DateTime.UtcNow);

        var result = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("Updated BagPolicy {PolicyId}", command.PolicyId);
        return result;
    }
}
