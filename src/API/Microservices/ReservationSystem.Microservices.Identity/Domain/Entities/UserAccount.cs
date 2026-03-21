namespace ReservationSystem.Microservices.Identity.Domain.Entities;

/// <summary>
/// Core domain entity representing a user account.
/// Contains identity and credential state. Has no dependency on infrastructure,
/// persistence, or serialisation concerns.
/// </summary>
public sealed class UserAccount
{
    public Guid UserAccountId { get; private set; }
    public Guid IdentityReference { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }
    public bool IsLocked { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset PasswordChangedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserAccount() { }

    /// <summary>
    /// Factory method for creating a brand-new user account. Assigns a new UserAccountId and timestamps.
    /// </summary>
    public static UserAccount Create(
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var now = DateTimeOffset.UtcNow;

        return new UserAccount
        {
            UserAccountId = Guid.NewGuid(),
            IdentityReference = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            IsEmailVerified = false,
            IsLocked = false,
            FailedLoginAttempts = 0,
            LastLoginAt = null,
            PasswordChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// Does not assign a new UserAccountId or reset timestamps.
    /// </summary>
    public static UserAccount Reconstitute(
        Guid userAccountId,
        Guid identityReference,
        string email,
        string passwordHash,
        bool isEmailVerified,
        bool isLocked,
        int failedLoginAttempts,
        DateTimeOffset? lastLoginAt,
        DateTimeOffset passwordChangedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new UserAccount
        {
            UserAccountId = userAccountId,
            IdentityReference = identityReference,
            Email = email,
            PasswordHash = passwordHash,
            IsEmailVerified = isEmailVerified,
            IsLocked = isLocked,
            FailedLoginAttempts = failedLoginAttempts,
            LastLoginAt = lastLoginAt,
            PasswordChangedAt = passwordChangedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LastLoginAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Lock()
    {
        IsLocked = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        PasswordChangedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeEmail(string newEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        Email = newEmail;
        IsEmailVerified = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
