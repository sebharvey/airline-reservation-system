using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class ReissueTicketsResponse
{
    [JsonPropertyName("voidedETicketNumbers")] public List<string> VoidedETicketNumbers { get; init; } = [];
    [JsonPropertyName("tickets")] public List<TicketSummary> Tickets { get; init; } = [];
}
