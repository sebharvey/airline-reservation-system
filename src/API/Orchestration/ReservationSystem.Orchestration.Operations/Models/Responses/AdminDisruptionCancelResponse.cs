using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminDisruptionCancelResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("affectedPassengerCount")]
    public int AffectedPassengerCount { get; init; }

    [JsonPropertyName("rebookedCount")]
    public int RebookedCount { get; init; }

    [JsonPropertyName("cancelledWithRefundCount")]
    public int CancelledWithRefundCount { get; init; }

    [JsonPropertyName("failedCount")]
    public int FailedCount { get; init; }

    [JsonPropertyName("outcomes")]
    public IReadOnlyList<DisruptionPassengerOutcome> Outcomes { get; init; } = [];

    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; }
}

public sealed class DisruptionPassengerOutcome
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = string.Empty; // "Rebooked" | "CancelledWithRefund" | "Failed"

    [JsonPropertyName("replacementFlightNumber")]
    public string? ReplacementFlightNumber { get; init; }

    [JsonPropertyName("replacementDepartureDate")]
    public string? ReplacementDepartureDate { get; init; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }
}
