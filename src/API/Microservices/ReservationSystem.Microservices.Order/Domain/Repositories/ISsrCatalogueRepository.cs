using ReservationSystem.Microservices.Order.Domain.Entities;

namespace ReservationSystem.Microservices.Order.Domain.Repositories;

public interface ISsrCatalogueRepository
{
    Task<IReadOnlyList<SsrCatalogueEntry>> GetActiveAsync(CancellationToken cancellationToken = default);
}
