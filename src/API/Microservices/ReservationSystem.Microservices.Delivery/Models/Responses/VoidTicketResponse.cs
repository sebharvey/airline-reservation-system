using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class VoidTicketResponse
{
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("voidedAt")] public DateTime? VoidedAt { get; init; }
}
