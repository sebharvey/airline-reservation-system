using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Seat.Models.Requests;

public sealed class CreateSeatmapRequest
{
    [JsonPropertyName("aircraftTypeCode")]
    public string AircraftTypeCode { get; init; } = string.Empty;

    [JsonPropertyName("cabinLayout")]
    public JsonElement CabinLayout { get; init; }
}
