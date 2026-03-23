namespace ReservationSystem.Microservices.Customer.Models.Requests;

/// <summary>
/// HTTP request body for searching customers. Sent as POST body to avoid PII in logs.
/// </summary>
public sealed class SearchCustomersRequest
{
    public string Query { get; init; } = string.Empty;
}
