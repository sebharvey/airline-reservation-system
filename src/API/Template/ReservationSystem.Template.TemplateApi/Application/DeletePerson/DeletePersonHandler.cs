using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.DeletePerson;

/// <summary>
/// Handles the <see cref="DeletePersonCommand"/>.
/// </summary>
public sealed class DeletePersonHandler
{
    private readonly IPersonRepository _repository;
    private readonly ILogger<DeletePersonHandler> _logger;

    public DeletePersonHandler(IPersonRepository repository, ILogger<DeletePersonHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns><c>true</c> if the Person was found and deleted; <c>false</c> if not found.</returns>
    public async Task<bool> HandleAsync(DeletePersonCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.PersonID, cancellationToken);

        if (!deleted)
            _logger.LogWarning("Delete requested for unknown Person {PersonID}", command.PersonID);
        else
            _logger.LogInformation("Deleted Person {PersonID}", command.PersonID);

        return deleted;
    }
}
