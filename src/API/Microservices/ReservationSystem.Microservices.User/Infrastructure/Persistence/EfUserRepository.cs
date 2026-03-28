using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.User.Domain.Repositories;

namespace ReservationSystem.Microservices.User.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUserRepository"/>.
///
/// Uses <see cref="UserDbContext"/> to interact with the user.User table.
/// The DbContext is scoped (one per function invocation) so no manual connection
/// management is required — EF handles connection lifetime internally.
/// </summary>
public sealed class EfUserRepository : IUserRepository
{
    private readonly UserDbContext _dbContext;
    private readonly ILogger<EfUserRepository> _logger;

    public EfUserRepository(UserDbContext dbContext, ILogger<EfUserRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Domain.Entities.User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<Domain.Entities.User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<Domain.Entities.User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Entities.User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(Domain.Entities.User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted User {UserId} into [user].[User]", user.UserId);
    }

    public async Task UpdateAsync(Domain.Entities.User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for User {UserId}", user.UserId);
        else
            _logger.LogDebug("Updated User {UserId} in [user].[User]", user.UserId);
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
            return false;

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted User {UserId} from [user].[User]", userId);
        return true;
    }
}
