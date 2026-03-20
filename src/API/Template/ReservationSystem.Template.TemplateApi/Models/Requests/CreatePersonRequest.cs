using System.Text.Json.Serialization;

namespace ReservationSystem.Template.TemplateApi.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/persons.
/// PersonID is required because [dbo].[Persons] has no IDENTITY constraint.
/// </summary>
public sealed class CreatePersonRequest
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
