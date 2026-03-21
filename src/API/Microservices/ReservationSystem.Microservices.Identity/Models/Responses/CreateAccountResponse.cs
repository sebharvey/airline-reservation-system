namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/accounts.
/// </summary>
public sealed class CreateAccountResponse
{
    public Guid IdentityReference { get; init; }
    public Guid UserAccountId { get; init; }
}
