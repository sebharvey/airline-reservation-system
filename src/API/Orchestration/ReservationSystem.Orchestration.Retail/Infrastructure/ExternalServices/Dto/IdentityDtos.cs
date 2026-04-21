namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising Identity microservice responses.
// These are not exposed beyond the infrastructure layer.

public sealed class IdentityVerifyTokenResponse
{
    public bool Valid { get; init; }
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
}
