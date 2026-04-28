using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.UpdateWatchlistEntry;

public sealed class UpdateWatchlistEntryHandler
{
    private readonly IWatchlistRepository _repository;
    private readonly ILogger<UpdateWatchlistEntryHandler> _logger;

    public UpdateWatchlistEntryHandler(IWatchlistRepository repository, ILogger<UpdateWatchlistEntryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<WatchlistEntry?> HandleAsync(
        UpdateWatchlistEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var entry = await _repository.GetByIdAsync(command.WatchlistId, cancellationToken);
        if (entry is null) return null;

        entry.Update(command.GivenName, command.Surname, command.DateOfBirth, command.PassportNumber, command.Notes);

        var updated = await _repository.UpdateAsync(entry, cancellationToken);
        _logger.LogInformation("Updated WatchlistEntry {WatchlistId}", command.WatchlistId);
        return updated;
    }
}
