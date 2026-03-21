using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

/// <summary>
/// HTTP response body for Ticket endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class TicketResponse
{
    [JsonPropertyName("ticketId")]
    public Guid TicketId { get; init; }

    [JsonPropertyName("manifestId")]
    public Guid ManifestId { get; init; }

    [JsonPropertyName("passengerId")]
    public Guid PassengerId { get; init; }

    [JsonPropertyName("segmentId")]
    public Guid SegmentId { get; init; }

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("ticketStatus")]
    public string TicketStatus { get; init; } = string.Empty;

    [JsonPropertyName("issuedAt")]
    public DateTimeOffset IssuedAt { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
