namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/accounts.
/// </summary>
public sealed class CreateAccountResponse
{
    public Guid UserAccountId { get; init; }
    public Guid IdentityReference { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
