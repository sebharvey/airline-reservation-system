using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class CreateBoardingCardsRequest
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengers")] public List<BoardingCardPassenger> Passengers { get; init; } = [];
}

public sealed class BoardingCardPassenger
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("inventoryIds")] public List<string> InventoryIds { get; init; } = [];
}
