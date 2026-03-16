using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.GetAllTemplateItems;

public sealed class GetAllTemplateItemsHandler
{
    private readonly ITemplateItemRepository _repository;
    private readonly ILogger<GetAllTemplateItemsHandler> _logger;

    public GetAllTemplateItemsHandler(
        ITemplateItemRepository repository,
        ILogger<GetAllTemplateItemsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TemplateItem>> HandleAsync(
        GetAllTemplateItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all TemplateItems");
        return await _repository.GetAllAsync(cancellationToken);
    }
}
