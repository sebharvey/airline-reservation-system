using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Application.UseCases.DeleteTemplateItem;

public sealed class DeleteTemplateItemHandler
{
    private readonly ITemplateItemRepository _repository;
    private readonly ILogger<DeleteTemplateItemHandler> _logger;

    public DeleteTemplateItemHandler(
        ITemplateItemRepository repository,
        ILogger<DeleteTemplateItemHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns><c>true</c> if the item was found and deleted; <c>false</c> if not found.</returns>
    public async Task<bool> HandleAsync(
        DeleteTemplateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            _logger.LogWarning("Delete requested for unknown TemplateItem {Id}", command.Id);
            return false;
        }

        await _repository.DeleteAsync(command.Id, cancellationToken);

        _logger.LogInformation("Deleted TemplateItem {Id}", command.Id);

        return true;
    }
}
