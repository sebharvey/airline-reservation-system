namespace ReservationSystem.Microservices.Offer.Domain.Repositories;

/// <summary>
/// Port (interface) for Offer persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The SQL implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface IOfferRepository
{
    Task<Entities.Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.Offer>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.Offer offer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.Offer offer, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
