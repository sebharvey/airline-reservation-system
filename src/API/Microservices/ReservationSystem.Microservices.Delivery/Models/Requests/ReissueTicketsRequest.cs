using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class ReissueTicketsRequest
{
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("voidedETicketNumbers")] public List<string> VoidedETicketNumbers { get; init; } = [];
    [JsonPropertyName("passengers")] public List<PassengerDetail> Passengers { get; init; } = [];
    [JsonPropertyName("segments")] public List<SegmentDetail> Segments { get; init; } = [];
    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;
    [JsonPropertyName("actor")] public string Actor { get; init; } = string.Empty;
}
