using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Seat.Models.Requests;

public sealed class UpdateSeatmapRequest
{
    [JsonPropertyName("cabinLayout")]
    public JsonElement? CabinLayout { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
