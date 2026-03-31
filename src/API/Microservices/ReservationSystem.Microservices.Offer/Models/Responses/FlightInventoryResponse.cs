using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FlightInventoryResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("cabins")]
    public IReadOnlyList<CabinInventoryResponse> Cabins { get; init; } = [];
}

public sealed class CabinInventoryResponse
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("seatsSold")]
    public int SeatsSold { get; init; }

    [JsonPropertyName("seatsHeld")]
    public int SeatsHeld { get; init; }
}
