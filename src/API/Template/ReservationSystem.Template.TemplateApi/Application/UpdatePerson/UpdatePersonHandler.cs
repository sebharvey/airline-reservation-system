using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.UpdatePerson;

/// <summary>
/// Handles the <see cref="UpdatePersonCommand"/>.
/// Loads the existing Person, applies the update via the domain method, and persists.
/// Returns null when the PersonID does not exist.
/// </summary>
public sealed class UpdatePersonHandler
{
    private readonly IPersonRepository _repository;
    private readonly ILogger<UpdatePersonHandler> _logger;

    public UpdatePersonHandler(IPersonRepository repository, ILogger<UpdatePersonHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns>The updated <see cref="Person"/>, or <c>null</c> if not found.</returns>
    public async Task<Person?> HandleAsync(UpdatePersonCommand command, CancellationToken cancellationToken = default)
    {
        var person = await _repository.GetByIdAsync(command.PersonID, cancellationToken);

        if (person is null)
        {
            _logger.LogWarning("Update requested for unknown Person {PersonID}", command.PersonID);
            return null;
        }

        person.Update(command.LastName, command.FirstName, command.Address, command.City);

        await _repository.UpdateAsync(person, cancellationToken);

        _logger.LogInformation("Updated Person {PersonID}", person.PersonID);

        return person;
    }
}
