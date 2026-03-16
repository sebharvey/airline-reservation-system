namespace ReservationSystem.Template.TemplateApi.Application.GetTemplateItem;

/// <summary>
/// Query to retrieve a single TemplateItem by its identifier.
/// Immutable record — queries carry no side effects.
/// </summary>
public sealed record GetTemplateItemQuery(Guid Id);
