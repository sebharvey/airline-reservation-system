using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminDisruptionRebookOrderResponse
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = string.Empty; // "Rebooked" | "Failed"

    [JsonPropertyName("replacementFlightNumber")]
    public string? ReplacementFlightNumber { get; init; }

    [JsonPropertyName("replacementDepartureDate")]
    public string? ReplacementDepartureDate { get; init; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }
}
