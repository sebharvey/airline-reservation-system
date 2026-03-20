namespace ReservationSystem.Template.TemplateApi.Application.UpdatePerson;

/// <summary>
/// Command carrying the data needed to update an existing Person record.
/// </summary>
public sealed record UpdatePersonCommand(
    int PersonID,
    string LastName,
    string? FirstName,
    string? Address,
    string? City);
