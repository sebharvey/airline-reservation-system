using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.GetPerson;

/// <summary>
/// Handles the <see cref="GetPersonQuery"/>.
/// Orchestrates domain and repository interactions; contains no SQL or HTTP concerns.
/// </summary>
public sealed class GetPersonHandler
{
    private readonly IPersonRepository _repository;
    private readonly ILogger<GetPersonHandler> _logger;

    public GetPersonHandler(IPersonRepository repository, ILogger<GetPersonHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Person?> HandleAsync(GetPersonQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching Person {PersonID}", query.PersonID);

        var person = await _repository.GetByIdAsync(query.PersonID, cancellationToken);

        if (person is null)
            _logger.LogWarning("Person {PersonID} not found", query.PersonID);

        return person;
    }
}
