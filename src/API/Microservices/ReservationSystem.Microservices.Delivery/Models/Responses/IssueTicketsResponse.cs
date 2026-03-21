using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class IssueTicketsResponse
{
    [JsonPropertyName("tickets")] public List<TicketSummary> Tickets { get; init; } = [];
}

public sealed class TicketSummary
{
    [JsonPropertyName("ticketId")] public Guid TicketId { get; init; }
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("segmentId")] public string SegmentId { get; init; } = string.Empty;
    [JsonPropertyName("flightNumber")] public string FlightNumber { get; init; } = string.Empty;
    [JsonPropertyName("departureDate")] public string DepartureDate { get; init; } = string.Empty;
}
