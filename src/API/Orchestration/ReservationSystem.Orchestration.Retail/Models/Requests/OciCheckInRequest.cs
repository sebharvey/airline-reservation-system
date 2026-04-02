using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class OciCheckInRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("passengers")]
    public List<OciCheckInPassengerRequest> Passengers { get; init; } = [];
}

public sealed class OciCheckInPassengerRequest
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryIds")]
    public List<string> InventoryIds { get; init; } = [];
}
