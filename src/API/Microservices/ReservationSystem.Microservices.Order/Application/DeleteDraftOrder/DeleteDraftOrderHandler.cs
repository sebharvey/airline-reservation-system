using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.DeleteDraftOrder;

/// <summary>
/// Handles the <see cref="DeleteDraftOrderCommand"/>.
/// Deletes an order by ID regardless of status. Used for draft clean-up and rollback
/// of confirmed orders when post-confirmation steps (e.g. ticket issuance) fail.
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

    /// <returns><c>true</c> if the order was found and deleted; <c>false</c> if not found.</returns>
    public async Task<bool> HandleAsync(DeleteDraftOrderCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting order {OrderId}", command.OrderId);

        var deleted = await _repository.DeleteOrderAsync(command.OrderId, cancellationToken);

        if (deleted)
            _logger.LogInformation("Order {OrderId} deleted", command.OrderId);
        else
            _logger.LogWarning("Order {OrderId} not found — nothing deleted", command.OrderId);

        return deleted;
    }
}
