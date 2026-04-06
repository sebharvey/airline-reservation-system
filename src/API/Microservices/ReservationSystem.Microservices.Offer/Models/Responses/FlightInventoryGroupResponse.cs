using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FlightInventoryGroupResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public int ArrivalDayOffset { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("f")]
    public CabinInventory? F { get; init; }

    [JsonPropertyName("j")]
    public CabinInventory? J { get; init; }

    [JsonPropertyName("w")]
    public CabinInventory? W { get; init; }

    [JsonPropertyName("y")]
    public CabinInventory? Y { get; init; }

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("totalSeatsAvailable")]
    public int TotalSeatsAvailable { get; init; }

    [JsonPropertyName("loadFactor")]
    public int LoadFactor { get; init; }

    [JsonPropertyName("ticketingStatus")]
    public string TicketingStatus { get; init; } = string.Empty;
}

public sealed class CabinInventory
{
    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("seatsSold")]
    public int SeatsSold { get; init; }

    [JsonPropertyName("seatsHeld")]
    public int SeatsHeld { get; init; }
}
