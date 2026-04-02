using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class CreateBoardingCardsResponse
{
    [JsonPropertyName("boardingCards")] public List<BoardingCardResponse> BoardingCards { get; init; } = [];
}

public sealed class BoardingCardResponse
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("givenName")] public string GivenName { get; init; } = string.Empty;
    [JsonPropertyName("surname")] public string Surname { get; init; } = string.Empty;
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDateTime")] public string DepartureDateTime { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("sequenceNumber")] public string SequenceNumber { get; init; } = string.Empty;
    [JsonPropertyName("bcbpString")] public string BcbpString { get; init; } = string.Empty;
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("gate")] public string Gate { get; init; } = string.Empty;
    [JsonPropertyName("boardingTime")] public string BoardingTime { get; init; } = string.Empty;
}
