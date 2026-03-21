using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;

public sealed class UpdateBagPolicyHandler
{
    private readonly IBagPolicyRepository _repository;
    private readonly ILogger<UpdateBagPolicyHandler> _logger;

    public UpdateBagPolicyHandler(IBagPolicyRepository repository, ILogger<UpdateBagPolicyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPolicy?> HandleAsync(
        UpdateBagPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
