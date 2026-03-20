using System.Text.Json.Serialization;

namespace ReservationSystem.Template.TemplateApi.Models.Responses;

/// <summary>
/// HTTP response body for Person endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class PersonResponse
{
    [JsonPropertyName("personId")]
    public int PersonID { get; init; }

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }
}
