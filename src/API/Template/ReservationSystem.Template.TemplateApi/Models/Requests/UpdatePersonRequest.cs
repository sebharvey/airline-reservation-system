using System.Text.Json.Serialization;

namespace ReservationSystem.Template.TemplateApi.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/persons/{personId}.
/// PersonID is taken from the URL path, not the body.
/// </summary>
public sealed class UpdatePersonRequest
{
    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }
}
