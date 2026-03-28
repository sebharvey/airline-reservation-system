namespace ReservationSystem.Microservices.User.Domain.Repositories;

/// <summary>
/// Port (interface) for User persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<Entities.User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Entities.User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<Entities.User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Entities.User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.User user, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.User user, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
