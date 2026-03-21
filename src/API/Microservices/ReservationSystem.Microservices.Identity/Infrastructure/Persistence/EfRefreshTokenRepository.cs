using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IRefreshTokenRepository"/>.
///
/// Uses <see cref="IdentityDbContext"/> to interact with the identity.RefreshToken table.
/// The DbContext is scoped (one per function invocation) so no manual connection
/// management is required — EF handles connection lifetime internally.
/// </summary>
public sealed class EfRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<EfRefreshTokenRepository> _logger;

    public EfRefreshTokenRepository(IdentityDbContext dbContext, ILogger<EfRefreshTokenRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetByUserAccountIdAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserAccountId == userAccountId)
            .ToListAsync(cancellationToken);

        return tokens.AsReadOnly();
    }

    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted RefreshToken {RefreshTokenId} into [identity].[RefreshToken]", refreshToken.RefreshTokenId);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        _dbContext.RefreshTokens.Update(refreshToken);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for RefreshToken {RefreshTokenId}", refreshToken.RefreshTokenId);
        else
            _logger.LogDebug("Updated RefreshToken {RefreshTokenId} in [identity].[RefreshToken]", refreshToken.RefreshTokenId);
    }

    public async Task RevokeAllForUserAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(r => r.UserAccountId == userAccountId && !r.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
            token.Revoke();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Revoked {Count} refresh tokens for UserAccount {UserAccountId}", tokens.Count, userAccountId);
    }
}
