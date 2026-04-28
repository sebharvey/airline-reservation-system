using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface IWatchlistRepository
{
    Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WatchlistEntry?> GetByIdAsync(Guid watchlistId, CancellationToken cancellationToken = default);
    Task<WatchlistEntry?> GetByPassportNumberAsync(string passportNumber, CancellationToken cancellationToken = default);
    Task<WatchlistEntry> CreateAsync(WatchlistEntry entry, CancellationToken cancellationToken = default);
    Task<WatchlistEntry?> UpdateAsync(WatchlistEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid watchlistId, CancellationToken cancellationToken = default);
}
