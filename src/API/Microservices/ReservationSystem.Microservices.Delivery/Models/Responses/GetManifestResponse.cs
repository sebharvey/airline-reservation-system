using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class GetManifestResponse
{
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("totalPassengers")] public int TotalPassengers { get; init; }
    [JsonPropertyName("entries")] public List<ManifestEntryDetail> Entries { get; init; } = [];
}

public sealed class ManifestEntryDetail
{
    [JsonPropertyName("manifestId")] public Guid ManifestId { get; init; }
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("ssrCodes")] public List<string> SsrCodes { get; init; } = [];
    [JsonPropertyName("departureTime")] public string DepartureTime { get; init; } = string.Empty;
    [JsonPropertyName("arrivalTime")] public string ArrivalTime { get; init; } = string.Empty;
    [JsonPropertyName("checkedIn")] public bool CheckedIn { get; init; }
    [JsonPropertyName("checkedInAt")] public DateTime? CheckedInAt { get; init; }
}
