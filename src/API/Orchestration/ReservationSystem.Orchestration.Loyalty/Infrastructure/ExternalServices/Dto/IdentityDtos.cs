namespace ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising Identity microservice responses.
// These are not exposed beyond the infrastructure layer.

public sealed class IdentityLoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public Guid UserAccountId { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public sealed class IdentityRefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class IdentityVerifyTokenResponse
{
    public bool Valid { get; init; }
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
}

public sealed class IdentityCreateAccountResponse
{
    public Guid UserAccountId { get; init; }
}

public sealed class IdentityAccountSummaryDto
{
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
}
