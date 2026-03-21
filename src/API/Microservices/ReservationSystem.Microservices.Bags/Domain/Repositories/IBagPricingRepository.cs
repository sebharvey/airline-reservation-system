namespace ReservationSystem.Microservices.Bags.Domain.Repositories;

/// <summary>
/// Port (interface) for BagPricing persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IBagPricingRepository
{
    Task<Entities.BagPricing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.BagPricing>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.BagPricing pricing, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.BagPricing pricing, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
