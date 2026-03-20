using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.CreatePerson;

/// <summary>
/// Handles the <see cref="CreatePersonCommand"/>.
/// Creates and persists a new <see cref="Person"/> via the domain factory.
/// Returns null if a Person with the same PersonID already exists (duplicate key guard).
/// </summary>
public sealed class CreatePersonHandler
{
    private readonly IPersonRepository _repository;
    private readonly ILogger<CreatePersonHandler> _logger;

    public CreatePersonHandler(IPersonRepository repository, ILogger<CreatePersonHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns>The created <see cref="Person"/>, or <c>null</c> if the PersonID already exists.</returns>
    public async Task<Person?> HandleAsync(CreatePersonCommand command, CancellationToken cancellationToken = default)
    {
        var exists = await _repository.ExistsAsync(command.PersonID, cancellationToken);

        if (exists)
        {
            _logger.LogWarning("Create rejected — Person {PersonID} already exists", command.PersonID);
            return null;
        }

        var person = Person.Create(command.PersonID, command.LastName, command.FirstName, command.Address, command.City);

        await _repository.CreateAsync(person, cancellationToken);

        _logger.LogInformation("Created Person {PersonID}", person.PersonID);

        return person;
    }
}
