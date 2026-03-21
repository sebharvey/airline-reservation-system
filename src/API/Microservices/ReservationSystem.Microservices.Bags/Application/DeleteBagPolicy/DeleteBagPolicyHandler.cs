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

    public async Task HandleAsync(
        DeleteBagPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
