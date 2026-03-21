namespace ReservationSystem.Microservices.Identity.Domain.Entities;

/// <summary>
/// Domain entity representing a refresh token issued to a user session.
/// Tracks token lifecycle including revocation and expiry.
/// </summary>
public sealed class RefreshToken
{
    public Guid RefreshTokenId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? DeviceHint { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RefreshToken() { }

    /// <summary>
    /// Factory method for issuing a new refresh token.
    /// </summary>
    public static RefreshToken Create(
        Guid userAccountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? deviceHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTimeOffset.UtcNow;

        return new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserAccountId = userAccountId,
            TokenHash = tokenHash,
            DeviceHint = deviceHint,
            IsRevoked = false,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting a refresh token from a persistence store.
    /// </summary>
    public static RefreshToken Reconstitute(
        Guid refreshTokenId,
        Guid userAccountId,
        string tokenHash,
        string? deviceHint,
        bool isRevoked,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new RefreshToken
        {
            RefreshTokenId = refreshTokenId,
            UserAccountId = userAccountId,
            TokenHash = tokenHash,
            DeviceHint = deviceHint,
            IsRevoked = isRevoked,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Revoke()
    {
        IsRevoked = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;
}
