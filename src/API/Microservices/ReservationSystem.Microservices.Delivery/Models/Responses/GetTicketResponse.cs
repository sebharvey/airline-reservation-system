using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class GetTicketResponse
{
    [JsonPropertyName("ticketId")] public Guid TicketId { get; init; }
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("voidedAt")] public DateTime? VoidedAt { get; init; }
    [JsonPropertyName("ticketData")] public JsonElement? TicketData { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
    [JsonPropertyName("version")] public int Version { get; init; }
}
