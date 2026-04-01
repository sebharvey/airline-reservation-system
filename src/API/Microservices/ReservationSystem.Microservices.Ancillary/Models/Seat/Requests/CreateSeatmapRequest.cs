using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;

public sealed class CreateSeatmapRequest
{
    [JsonPropertyName("aircraftTypeCode")]
    public string AircraftTypeCode { get; init; } = string.Empty;

    [JsonPropertyName("cabinLayout")]
    public JsonElement CabinLayout { get; init; }
}
