namespace ReservationSystem.Template.TemplateApi.Models.Database;

/// <summary>
/// Entity Framework Core row model for the <c>[template].[Items]</c> table.
///
/// The <see cref="Attributes"/> column is a raw JSON string; deserialisation into a typed
/// object is handled by the repository after EF returns the record
/// (see <see cref="JsonFields.TemplateItemAttributes"/>).
/// </summary>
public sealed class TemplateItemRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Raw JSON from the <c>Attributes</c> column.
    /// Deserialise via <see cref="JsonFields.TemplateItemAttributes"/> before use.
    /// </summary>
    public string? Attributes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
