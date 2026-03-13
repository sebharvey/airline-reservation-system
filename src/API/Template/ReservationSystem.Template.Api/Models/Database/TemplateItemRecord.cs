namespace ReservationSystem.Template.Api.Models.Database;

/// <summary>
/// Dapper row model for the <c>[template].[Items]</c> table.
///
/// Column names match the SQL schema exactly so Dapper can bind them without
/// custom type handlers. The <see cref="Attributes"/> column is a raw JSON
/// string; deserialisation into a typed object is handled by the repository
/// after Dapper returns the record (see <see cref="JsonFields.TemplateItemAttributes"/>).
/// </summary>
public sealed class TemplateItemRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Raw JSON from the <c>Attributes</c> column.
    /// Deserialise via <see cref="JsonFields.TemplateItemAttributes"/> before use.
    /// </summary>
    public string? Attributes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
