using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class UpdateSeatStatusRequest
{
    [JsonPropertyName("updates")]
    public IReadOnlyList<SeatStatusUpdateItem> Updates { get; init; } = [];
}

public sealed class SeatStatusUpdateItem
{
    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
