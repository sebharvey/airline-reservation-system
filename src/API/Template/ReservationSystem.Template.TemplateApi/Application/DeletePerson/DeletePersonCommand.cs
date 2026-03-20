namespace ReservationSystem.Template.TemplateApi.Application.DeletePerson;

/// <summary>
/// Command to delete a Person by PersonID.
/// </summary>
public sealed record DeletePersonCommand(int PersonID);
