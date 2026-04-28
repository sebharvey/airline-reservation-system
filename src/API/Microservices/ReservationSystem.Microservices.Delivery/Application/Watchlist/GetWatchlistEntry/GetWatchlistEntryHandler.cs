using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.GetWatchlistEntry;

public sealed class GetWatchlistEntryHandler
{
    private readonly IWatchlistRepository _repository;

    public GetWatchlistEntryHandler(IWatchlistRepository repository)
    {
        _repository = repository;
    }

    public async Task<WatchlistEntry?> HandleAsync(
        GetWatchlistEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.WatchlistId, cancellationToken);
    }
}
