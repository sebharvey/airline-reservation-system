using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
using ReservationSystem.Template.TemplateApi.Domain.ValueObjects;

namespace ReservationSystem.Template.TemplateApi.Application.UseCases.CreateTemplateItem;

/// <summary>
/// Handles the <see cref="CreateTemplateItemCommand"/>.
/// Creates and persists a new <see cref="TemplateItem"/> via the domain factory.
/// </summary>
public sealed class CreateTemplateItemHandler
{
    private readonly ITemplateItemRepository _repository;
    private readonly ILogger<CreateTemplateItemHandler> _logger;

    public CreateTemplateItemHandler(
        ITemplateItemRepository repository,
        ILogger<CreateTemplateItemHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<TemplateItem> HandleAsync(
        CreateTemplateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var metadata = new TemplateItemMetadata(
            command.Tags,
            command.Priority,
            command.Properties);

        var item = TemplateItem.Create(command.Name, metadata);

        await _repository.CreateAsync(item, cancellationToken);

        _logger.LogInformation("Created TemplateItem {Id}", item.Id);

        return item;
    }
}
