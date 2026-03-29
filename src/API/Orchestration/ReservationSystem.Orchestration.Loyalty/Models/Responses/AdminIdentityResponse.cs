namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

/// <summary>
/// Identity account details returned as part of the admin customer response.
/// PasswordHash is never included.
/// </summary>
public sealed class AdminIdentityResponse
{
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
    public bool IsLocked { get; init; }
    public int FailedLoginAttempts { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime PasswordChangedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
