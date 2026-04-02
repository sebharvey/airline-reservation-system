using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class GetBoardingCardsRequest
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
}
