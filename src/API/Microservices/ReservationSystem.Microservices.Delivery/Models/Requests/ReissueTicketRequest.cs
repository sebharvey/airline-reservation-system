using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/manifests/{manifestId}/tickets/{ticketId}/reissue.
/// </summary>
public sealed class ReissueTicketRequest
{
    [JsonPropertyName("newETicketNumber")]
    public string NewETicketNumber { get; init; } = string.Empty;
}
