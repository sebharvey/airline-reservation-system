using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class UpdateManifestSeatRequest
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("updates")] public List<SeatUpdateEntry> Updates { get; init; } = [];
}

public sealed class SeatUpdateEntry
{
    [JsonPropertyName("inventoryId")] public Guid InventoryId { get; init; }
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("version")] public int Version { get; init; }
}
