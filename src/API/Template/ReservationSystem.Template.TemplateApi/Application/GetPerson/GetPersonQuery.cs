namespace ReservationSystem.Template.TemplateApi.Application.GetPerson;

/// <summary>
/// Query to retrieve a single Person by their PersonID.
/// Immutable record — queries carry no side effects.
/// </summary>
public sealed record GetPersonQuery(int PersonID);
