namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising User microservice responses.
// These are not exposed beyond the infrastructure layer.

public sealed class UserLoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public sealed class UserMsResponse
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsLocked { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class AddUserMsResponse
{
    public Guid UserId { get; init; }
}
