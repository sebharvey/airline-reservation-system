using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.DeleteExpiredManifestItems;

public sealed class DeleteExpiredManifestItemsHandler
{
    private readonly IManifestRepository _repository;
    private readonly ILogger<DeleteExpiredManifestItemsHandler> _logger;

    public DeleteExpiredManifestItemsHandler(
        IManifestRepository repository,
        ILogger<DeleteExpiredManifestItemsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired manifest item cleanup");

        var deletedCount = await _repository.DeleteExpiredManifestItemsAsync(cancellationToken);

        _logger.LogInformation("Expired manifest item cleanup complete. Deleted {Count} manifest item(s)", deletedCount);

        return deletedCount;
    }
}
