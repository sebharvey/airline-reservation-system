using ReservationSystem.Microservices.Identity.Domain.Entities;

namespace ReservationSystem.Microservices.Identity.Domain.Repositories;

/// <summary>
/// Port (interface) for UserAccount persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The EF implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByPasswordResetTokenAsync(Guid token, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByEmailResetTokenAsync(Guid token, CancellationToken cancellationToken = default);

    Task CreateAsync(UserAccount userAccount, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserAccount userAccount, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userAccountId, CancellationToken cancellationToken = default);
}
