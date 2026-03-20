using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.GetAllPersons;

/// <summary>
/// Handles the <see cref="GetAllPersonsQuery"/>.
/// </summary>
public sealed class GetAllPersonsHandler
{
    private readonly IPersonRepository _repository;
    private readonly ILogger<GetAllPersonsHandler> _logger;

    public GetAllPersonsHandler(IPersonRepository repository, ILogger<GetAllPersonsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Person>> HandleAsync(
        GetAllPersonsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all Persons");
        return await _repository.GetAllAsync(cancellationToken);
    }
}
