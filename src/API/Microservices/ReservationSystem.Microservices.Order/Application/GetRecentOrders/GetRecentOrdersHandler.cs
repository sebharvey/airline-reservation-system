using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetRecentOrders;

/// <summary>
/// Handles the <see cref="GetRecentOrdersQuery"/>.
/// Retrieves the most recently created orders.
/// </summary>
public sealed class GetRecentOrdersHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetRecentOrdersHandler> _logger;

    public GetRecentOrdersHandler(
        IOrderRepository repository,
        ILogger<GetRecentOrdersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.Order>> HandleAsync(
        GetRecentOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving {Limit} most recent orders", query.Limit);

        return await _repository.GetRecentAsync(query.Limit, cancellationToken);
    }
}
