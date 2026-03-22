using ReservationSystem.Microservices.Identity.Domain.Entities;

namespace ReservationSystem.Microservices.Identity.Domain.Repositories;

/// <summary>
/// Port (interface) for RefreshToken persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The EF implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetByUserAccountIdAsync(Guid userAccountId, CancellationToken cancellationToken = default);

    Task CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userAccountId, CancellationToken cancellationToken = default);

    Task DeleteAllForUserAsync(Guid userAccountId, CancellationToken cancellationToken = default);
}
