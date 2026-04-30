using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.QueryOrders;

/// <summary>
/// Handles the <see cref="QueryOrdersQuery"/>.
/// Retrieves all orders for a specific flight.
/// </summary>
public sealed class QueryOrdersHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<QueryOrdersHandler> _logger;

    public QueryOrdersHandler(
        IOrderRepository repository,
        ILogger<QueryOrdersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.Order>> HandleAsync(
        QueryOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying orders for flight {FlightNumber} on {DepartureDate} with status {Status}",
            query.FlightNumber, query.DepartureDate, query.Status);

        return await _repository.GetByFlightAsync(query.FlightNumber, query.DepartureDate, query.Status, cancellationToken);
    }
}
