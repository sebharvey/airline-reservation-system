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
    public DateTime? LastLoginAt { get; private set; }
    public DateTime PasswordChangedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

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

        var now = DateTime.UtcNow;

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
        DateTime? lastLoginAt,
        DateTime passwordChangedAt,
        DateTime createdAt,
        DateTime updatedAt)
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
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Lock()
    {
        IsLocked = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        PasswordChangedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeEmail(string newEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        Email = newEmail;
        IsEmailVerified = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
