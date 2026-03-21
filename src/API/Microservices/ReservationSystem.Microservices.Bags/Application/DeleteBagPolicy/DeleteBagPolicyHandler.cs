using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.DeleteBagPolicy;

public sealed class DeleteBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<DeleteBagPolicyHandler> _logger;

    public DeleteBagPolicyHandler(IBagPolicyRepository repository, ILogger<DeleteBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteBagPolicyCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.PolicyId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted BagPolicy {PolicyId}", command.PolicyId);
        else
            _logger.LogWarning("Delete requested for unknown BagPolicy {PolicyId}", command.PolicyId);
        return deleted;
    }
}
