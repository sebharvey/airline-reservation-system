using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class CreateBoardingCardsResponse
{
    [JsonPropertyName("boardingCards")] public List<BoardingCardResponse> BoardingCards { get; init; } = [];
}

public sealed class BoardingCardResponse
{
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
    [JsonPropertyName("seatNumber")] public string SeatNumber { get; init; } = string.Empty;
    [JsonPropertyName("cabinCode")] public string CabinCode { get; init; } = string.Empty;
    [JsonPropertyName("sequenceNumber")] public string SequenceNumber { get; init; } = string.Empty;
    [JsonPropertyName("bcbpString")] public string BcbpString { get; init; } = string.Empty;
    [JsonPropertyName("passengerName")] public string PassengerName { get; init; } = string.Empty;
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
}
