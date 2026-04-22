using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class RebookManifestFlightRequest
{
    [JsonPropertyName("toInventoryId")]
    public Guid ToInventoryId { get; init; }

    [JsonPropertyName("toFlightNumber")]
    public string ToFlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("toOrigin")]
    public string ToOrigin { get; init; } = string.Empty;

    [JsonPropertyName("toDestination")]
    public string ToDestination { get; init; } = string.Empty;

    [JsonPropertyName("toDepartureDate")]
    public string ToDepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("toDepartureTime")]
    public string ToDepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("toArrivalTime")]
    public string ToArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("toCabinCode")]
    public string ToCabinCode { get; init; } = string.Empty;

    [JsonPropertyName("passengers")]
    public IReadOnlyList<RebookManifestPassengerRequest> Passengers { get; init; } = [];
}

public sealed class RebookManifestPassengerRequest
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;
}
