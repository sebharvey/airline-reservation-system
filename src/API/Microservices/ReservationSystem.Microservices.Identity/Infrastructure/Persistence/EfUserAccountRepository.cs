using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUserAccountRepository"/>.
///
/// Uses <see cref="IdentityDbContext"/> to interact with the identity.UserAccount table.
/// The DbContext is scoped (one per function invocation) so no manual connection
/// management is required — EF handles connection lifetime internally.
/// </summary>
public sealed class EfUserAccountRepository : IUserAccountRepository
{
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<EfUserAccountRepository> _logger;

    public EfUserAccountRepository(IdentityDbContext dbContext, ILogger<EfUserAccountRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserAccountId == userAccountId, cancellationToken);
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<UserAccount?> GetByPasswordResetTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PasswordResetToken == token, cancellationToken);
    }

    public async Task<UserAccount?> GetByEmailResetTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailResetToken == token, cancellationToken);
    }

    public async Task CreateAsync(UserAccount userAccount, CancellationToken cancellationToken = default)
    {
        _dbContext.UserAccounts.Add(userAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted UserAccount {UserAccountId} into [identity].[UserAccount]", userAccount.UserAccountId);
    }

    public async Task UpdateAsync(UserAccount userAccount, CancellationToken cancellationToken = default)
    {
        _dbContext.UserAccounts.Update(userAccount);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for UserAccount {UserAccountId}", userAccount.UserAccountId);
        else
            _logger.LogDebug("Updated UserAccount {UserAccountId} in [identity].[UserAccount]", userAccount.UserAccountId);
    }

    public async Task DeleteAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var userAccount = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.UserAccountId == userAccountId, cancellationToken);

        if (userAccount is null)
        {
            _logger.LogWarning("DeleteAsync found no row for UserAccount {UserAccountId}", userAccountId);
            return;
        }

        _dbContext.UserAccounts.Remove(userAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted UserAccount {UserAccountId} from [identity].[UserAccount]", userAccountId);
    }
}
