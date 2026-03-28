namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising User microservice responses.
// These are not exposed beyond the infrastructure layer.

public sealed class UserLoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
}
