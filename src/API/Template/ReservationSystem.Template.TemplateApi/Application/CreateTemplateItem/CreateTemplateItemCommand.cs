namespace ReservationSystem.Template.TemplateApi.Application.CreateTemplateItem;

/// <summary>
/// Command carrying the data needed to create a new TemplateItem.
/// Immutable record — the application layer maps HTTP request models to this
/// before passing it to the handler, keeping the handler free of HTTP concerns.
/// </summary>
public sealed record CreateTemplateItemCommand(
    string Name,
    IReadOnlyList<string> Tags,
    string Priority,
    IReadOnlyDictionary<string, string> Properties);
