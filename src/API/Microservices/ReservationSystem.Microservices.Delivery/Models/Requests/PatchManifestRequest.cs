using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class PatchManifestRequest
{
    [JsonPropertyName("updates")] public List<ManifestPatchEntry> Updates { get; init; } = [];
}

public sealed class ManifestPatchEntry
{
    [JsonPropertyName("inventoryId")] public Guid InventoryId { get; init; }
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("checkedIn")] public bool? CheckedIn { get; init; }
    [JsonPropertyName("checkedInAt")] public string? CheckedInAt { get; init; }
    [JsonPropertyName("ssrCodes")] public List<string>? SsrCodes { get; init; }
    [JsonPropertyName("version")] public int Version { get; init; }
}
