using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class UpdateCountResponse
{
    [JsonPropertyName("updated")] public int? Updated { get; init; }
    [JsonPropertyName("deleted")] public int? Deleted { get; init; }
}
