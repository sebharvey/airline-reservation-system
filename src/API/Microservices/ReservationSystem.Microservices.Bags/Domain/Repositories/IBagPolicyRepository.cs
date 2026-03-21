namespace ReservationSystem.Microservices.Bags.Domain.Repositories;

/// <summary>
/// Port (interface) for BagPolicy persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IBagPolicyRepository
{
    Task<Entities.BagPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.BagPolicy policy, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.BagPolicy policy, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
