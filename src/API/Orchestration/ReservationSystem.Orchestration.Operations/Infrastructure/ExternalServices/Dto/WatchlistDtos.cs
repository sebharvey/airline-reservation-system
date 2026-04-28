using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class WatchlistEntryDto
{
    [JsonPropertyName("watchlistId")]
    public Guid WatchlistId { get; set; }

    [JsonPropertyName("givenName")]
    public string GivenName { get; set; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; set; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string DateOfBirth { get; set; } = string.Empty;

    [JsonPropertyName("passportNumber")]
    public string PassportNumber { get; set; } = string.Empty;

    [JsonPropertyName("addedBy")]
    public string AddedBy { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class WatchlistEntriesListDto
{
    [JsonPropertyName("entries")]
    public IReadOnlyList<WatchlistEntryDto> Entries { get; set; } = [];
}
