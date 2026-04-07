using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class HoldInventoryRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("passengers")]
    public IReadOnlyList<PaxHoldItem> Passengers { get; init; } = [];

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }
}

public sealed class PaxHoldItem
{
    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("passengerId")]
    public string? PassengerId { get; init; }
}
