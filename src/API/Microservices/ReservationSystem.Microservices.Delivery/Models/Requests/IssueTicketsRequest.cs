using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class IssueTicketsRequest
{
    [JsonPropertyName("basketId")] public Guid BasketId { get; init; }
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengers")] public List<PassengerDetail> Passengers { get; init; } = [];
    [JsonPropertyName("segments")] public List<SegmentDetail> Segments { get; init; } = [];
}

public sealed class PassengerDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("dateOfBirth")] public string? DateOfBirth { get; init; }
}

public sealed class SegmentDetail
{
    [JsonPropertyName("segmentId")] public string SegmentId { get; init; } = string.Empty;
    [JsonPropertyName("inventoryId")] public Guid InventoryId { get; init; }
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("fareBasisCode")] public string FareBasisCode { get; init; } = string.Empty;
    [JsonPropertyName("seatAssignments")] public List<SeatAssignmentDetail>? SeatAssignments { get; init; }
    [JsonPropertyName("ssrCodes")] public List<SsrCodeDetail>? SsrCodes { get; init; }
}

public sealed class SeatAssignmentDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("positionType")] public string PositionType { get; init; } = string.Empty;
    [JsonPropertyName("deckCode")] public string DeckCode { get; init; } = string.Empty;
}

public sealed class SsrCodeDetail
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("segmentRef")] public string SegmentRef { get; init; } = string.Empty;
}
