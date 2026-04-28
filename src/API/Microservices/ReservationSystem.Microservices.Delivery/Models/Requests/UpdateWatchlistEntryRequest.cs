using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class UpdateWatchlistEntryRequest
{
    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
