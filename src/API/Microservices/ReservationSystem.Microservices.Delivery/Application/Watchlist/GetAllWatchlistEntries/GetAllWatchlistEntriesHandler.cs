using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.GetAllWatchlistEntries;

public sealed class GetAllWatchlistEntriesHandler
{
    private readonly IWatchlistRepository _repository;

    public GetAllWatchlistEntriesHandler(IWatchlistRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WatchlistEntry>> HandleAsync(
        GetAllWatchlistEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
