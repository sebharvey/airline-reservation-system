using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminAutoAssignSeatsResponse
{
    [JsonPropertyName("assigned")]
    public int Assigned { get; init; }

    [JsonPropertyName("failed")]
    public int Failed { get; init; }

    [JsonPropertyName("outcomes")]
    public IReadOnlyList<SeatAssignmentOutcomeResponse> Outcomes { get; init; } = [];
}

public sealed class SeatAssignmentOutcomeResponse
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }
}
