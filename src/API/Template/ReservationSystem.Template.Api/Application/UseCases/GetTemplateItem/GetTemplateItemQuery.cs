namespace ReservationSystem.Template.Api.Application.UseCases.GetTemplateItem;

/// <summary>
/// Query to retrieve a single TemplateItem by its identifier.
/// Immutable record — queries carry no side effects.
/// </summary>
public sealed record GetTemplateItemQuery(Guid Id);
