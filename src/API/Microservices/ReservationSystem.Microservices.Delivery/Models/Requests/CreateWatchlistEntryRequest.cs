using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class CreateWatchlistEntryRequest
{
    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; set; }

    [JsonPropertyName("addedBy")]
    public string? AddedBy { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
