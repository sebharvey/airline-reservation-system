using ReservationSystem.Template.TemplateApi.Application.CreatePerson;
using ReservationSystem.Template.TemplateApi.Application.UpdatePerson;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Models.Requests;
using ReservationSystem.Template.TemplateApi.Models.Responses;

namespace ReservationSystem.Template.TemplateApi.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of a Person.
///
/// Mapping directions:
///   HTTP create request  →  CreatePersonCommand
///   HTTP update request  →  UpdatePersonCommand
///   Domain entity        →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class PersonMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreatePersonCommand ToCreateCommand(CreatePersonRequest request) =>
        new(
            PersonID: request.PersonID,
            LastName: request.LastName,
            FirstName: request.FirstName,
            Address: request.Address,
            City: request.City);

    public static UpdatePersonCommand ToUpdateCommand(int personId, UpdatePersonRequest request) =>
        new(
            PersonID: personId,
            LastName: request.LastName,
            FirstName: request.FirstName,
            Address: request.Address,
            City: request.City);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static PersonResponse ToResponse(Person person) =>
        new()
        {
            PersonID = person.PersonID,
            LastName = person.LastName,
            FirstName = person.FirstName,
            Address = person.Address,
            City = person.City
        };

    public static IReadOnlyList<PersonResponse> ToResponse(IEnumerable<Person> persons) =>
        persons.Select(ToResponse).ToList().AsReadOnly();
}
