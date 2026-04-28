using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.DeleteWatchlistEntry;

public sealed class DeleteWatchlistEntryHandler
{
    private readonly IWatchlistRepository _repository;
    private readonly ILogger<DeleteWatchlistEntryHandler> _logger;

    public DeleteWatchlistEntryHandler(IWatchlistRepository repository, ILogger<DeleteWatchlistEntryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DeleteWatchlistEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.WatchlistId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted WatchlistEntry {WatchlistId}", command.WatchlistId);
        return deleted;
    }
}
