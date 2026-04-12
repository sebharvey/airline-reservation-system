using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.DeleteDraftOrder;

/// <summary>
/// Handles the <see cref="DeleteDraftOrderCommand"/>.
/// Deletes a single Draft order by ID. Only Draft orders may be deleted via this handler;
/// confirmed or cancelled orders are not affected.
/// </summary>
public sealed class DeleteDraftOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<DeleteDraftOrderHandler> _logger;

    public DeleteDraftOrderHandler(
        IOrderRepository repository,
        ILogger<DeleteDraftOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns><c>true</c> if the order was found and deleted; <c>false</c> if not found or not in Draft status.</returns>
    public async Task<bool> HandleAsync(DeleteDraftOrderCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting draft order {OrderId}", command.OrderId);

        var deleted = await _repository.DeleteDraftOrderAsync(command.OrderId, cancellationToken);

        if (deleted)
            _logger.LogInformation("Draft order {OrderId} deleted", command.OrderId);
        else
            _logger.LogWarning("Draft order {OrderId} not found or not in Draft status — nothing deleted", command.OrderId);

        return deleted;
    }
}
