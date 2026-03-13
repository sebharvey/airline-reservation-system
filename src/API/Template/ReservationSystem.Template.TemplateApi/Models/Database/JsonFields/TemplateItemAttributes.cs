using System.Text.Json.Serialization;

namespace ReservationSystem.Template.TemplateApi.Models.Database.JsonFields;

/// <summary>
/// Deserialisation target for the <c>Attributes</c> JSON column in
/// <c>[template].[Items]</c>.
///
/// This class exists purely to model the JSON stored in the database.
/// It carries <see cref="JsonPropertyNameAttribute"/> annotations to control
/// the on-disk JSON shape independently of C# naming conventions.
///
/// Example JSON:
/// <code>
/// {
///   "tags": ["example", "template"],
///   "priority": "high",
///   "properties": {
///     "source": "api",
///     "version": "1.0"
///   }
/// }
/// </code>
///
/// Mapping chain:
///   SQL JSON string
///   → <see cref="TemplateItemAttributes"/>        (Infrastructure — deserialise)
///   → <see cref="Domain.ValueObjects.TemplateItemMetadata"/>  (Domain — business logic)
///   → <see cref="TemplateItemAttributes"/>        (Infrastructure — serialise, on write)
/// </summary>
public sealed class TemplateItemAttributes
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";

    /// <summary>
    /// Arbitrary key/value pairs for extensibility without schema changes.
    /// Add dedicated typed fields here as requirements solidify.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = [];
}
