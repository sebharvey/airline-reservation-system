using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

/// <summary>
/// Port (interface) for Manifest persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IManifestRepository
{
    Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default);

    Task CreateAsync(Manifest manifest, CancellationToken cancellationToken = default);

    Task UpdateAsync(Manifest manifest, CancellationToken cancellationToken = default);
}
