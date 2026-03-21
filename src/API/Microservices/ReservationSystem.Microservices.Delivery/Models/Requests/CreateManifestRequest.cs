using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class CreateManifestRequest
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("entries")] public List<ManifestEntryRequest> Entries { get; init; } = [];
}

public sealed class ManifestEntryRequest
{
    [JsonPropertyName("ticketId")] public Guid TicketId { get; init; }
    [JsonPropertyName("inventoryId")] public Guid InventoryId { get; init; }
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("departureTime")] public string DepartureTime { get; init; } = string.Empty;
    [JsonPropertyName("arrivalTime")] public string ArrivalTime { get; init; } = string.Empty;
    [JsonPropertyName("aircraftType")] public string AircraftType { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("ssrCodes")] public List<string>? SsrCodes { get; init; }
}
