using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.CreateWatchlistEntry;

public sealed class CreateWatchlistEntryHandler
{
    private readonly IWatchlistRepository _repository;
    private readonly ILogger<CreateWatchlistEntryHandler> _logger;

    public CreateWatchlistEntryHandler(IWatchlistRepository repository, ILogger<CreateWatchlistEntryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<WatchlistEntry> HandleAsync(
        CreateWatchlistEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var entry = WatchlistEntry.Create(
            command.GivenName,
            command.Surname,
            command.DateOfBirth,
            command.PassportNumber,
            command.AddedBy,
            command.Notes);

        var created = await _repository.CreateAsync(entry, cancellationToken);
        _logger.LogInformation("Created WatchlistEntry {WatchlistId} for passport {PassportNumber}", created.WatchlistId, command.PassportNumber);
        return created;
    }
}
