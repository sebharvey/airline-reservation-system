namespace ReservationSystem.Microservices.User.Domain.Entities;

/// <summary>
/// Core domain entity representing an Apex Air employee user account.
/// Stores login credentials and account state for the reservation system.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class User
{
    public Guid UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private User() { }

    /// <summary>
    /// Factory method for creating a brand-new user account.
    /// </summary>
    public static User Create(
        string username,
        string email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var now = DateTime.UtcNow;

        return new User
        {
            UserId = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            IsLocked = false,
            FailedLoginAttempts = 0,
            LastLoginAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from the persistence store.
    /// </summary>
    public static User Reconstitute(
        Guid userId,
        string username,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        bool isActive,
        bool isLocked,
        int failedLoginAttempts,
        DateTime? lastLoginAt,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new User
        {
            UserId = userId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            IsActive = isActive,
            IsLocked = isLocked,
            FailedLoginAttempts = failedLoginAttempts,
            LastLoginAt = lastLoginAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
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

    public void UpdateProfile(string? firstName, string? lastName, string? email)
    {
        if (firstName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
            FirstName = firstName;
        }

        if (lastName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
            LastName = lastName;
        }

        if (email is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);
            Email = email;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetPassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        IsLocked = false;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }
}
