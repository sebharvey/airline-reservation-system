namespace ReservationSystem.Template.TemplateApi.Application.CreatePerson;

/// <summary>
/// Command carrying the data needed to create a new Person record.
/// PersonID is provided by the caller — the [dbo].[Persons] table has no IDENTITY constraint.
/// </summary>
public sealed record CreatePersonCommand(
    int PersonID,
    string LastName,
    string? FirstName,
    string? Address,
    string? City);
