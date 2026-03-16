using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.GetTemplateItem;

/// <summary>
/// Handles the <see cref="GetTemplateItemQuery"/>.
/// Orchestrates domain and repository interactions; contains no SQL or HTTP concerns.
/// </summary>
public sealed class GetTemplateItemHandler
{
    private readonly ITemplateItemRepository _repository;
    private readonly ILogger<GetTemplateItemHandler> _logger;

    public GetTemplateItemHandler(
        ITemplateItemRepository repository,
        ILogger<GetTemplateItemHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<TemplateItem?> HandleAsync(
        GetTemplateItemQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching TemplateItem {Id}", query.Id);

        var item = await _repository.GetByIdAsync(query.Id, cancellationToken);

        if (item is null)
            _logger.LogWarning("TemplateItem {Id} not found", query.Id);

        return item;
    }
}
