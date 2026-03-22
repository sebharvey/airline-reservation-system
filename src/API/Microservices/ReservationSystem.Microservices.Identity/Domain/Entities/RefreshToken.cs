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
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private RefreshToken() { }

    /// <summary>
    /// Factory method for issuing a new refresh token.
    /// </summary>
    public static RefreshToken Create(
        Guid userAccountId,
        string tokenHash,
        DateTime expiresAt,
        string? deviceHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTime.UtcNow;

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
        DateTime expiresAt,
        DateTime createdAt,
        DateTime updatedAt)
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
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;
}
