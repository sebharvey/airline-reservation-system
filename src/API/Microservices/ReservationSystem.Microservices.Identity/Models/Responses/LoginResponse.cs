namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/auth/login.
/// </summary>
public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public Guid UserAccountId { get; init; }
    public Guid IdentityReference { get; init; }
    public string Email { get; init; } = string.Empty;
}
