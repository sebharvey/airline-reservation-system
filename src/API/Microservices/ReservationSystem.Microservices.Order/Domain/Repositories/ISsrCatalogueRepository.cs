using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Domain.Repositories;

public interface ISsrCatalogueRepository
{
    Task<IReadOnlyList<SsrCatalogueEntry>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<SsrCatalogueEntry?> GetByCodeAsync(string ssrCode, CancellationToken cancellationToken = default);
    Task<SsrCatalogueEntry> CreateAsync(SsrCatalogueEntry entry, CancellationToken cancellationToken = default);
    Task<SsrCatalogueEntry?> UpdateAsync(string ssrCode, string label, string category, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(string ssrCode, CancellationToken cancellationToken = default);
}
