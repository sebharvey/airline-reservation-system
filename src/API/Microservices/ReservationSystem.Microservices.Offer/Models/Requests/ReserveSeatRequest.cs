using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class ReserveSeatRequest
{
    [JsonPropertyName("seatNumbers")]
    public IReadOnlyList<string> SeatNumbers { get; init; } = [];

    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }
}
