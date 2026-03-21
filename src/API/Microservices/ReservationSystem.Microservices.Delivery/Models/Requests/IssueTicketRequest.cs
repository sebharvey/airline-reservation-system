using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/manifests/{manifestId}/tickets.
/// </summary>
public sealed class IssueTicketRequest
{
    [JsonPropertyName("passengerId")]
    public Guid PassengerId { get; init; }

    [JsonPropertyName("segmentId")]
    public Guid SegmentId { get; init; }

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;
}
