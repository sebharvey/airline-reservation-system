namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/accounts/{identityReference:guid}/email/change-request.
/// </summary>
public sealed class EmailChangeRequest
{
    public string NewEmail { get; init; } = string.Empty;
}
