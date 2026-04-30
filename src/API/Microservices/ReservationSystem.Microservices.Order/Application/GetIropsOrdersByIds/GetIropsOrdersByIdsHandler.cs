using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.GetIropsOrders;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetIropsOrdersByIds;

/// <summary>
/// Handles the <see cref="GetIropsOrdersByIdsQuery"/>.
/// Batch-fetches specific orders projected for IROPS processing, identified by OrderIds.
/// </summary>
public sealed class GetIropsOrdersByIdsHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetIropsOrdersByIdsHandler> _logger;

    public GetIropsOrdersByIdsHandler(
        IOrderRepository repository,
        ILogger<GetIropsOrdersByIdsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<object?>> HandleAsync(
        GetIropsOrdersByIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching IROPS orders by IDs for flight {FlightNumber} on {DepartureDate}",
            query.FlightNumber, query.DepartureDate);

        var orders = await _repository.GetByIdsAsync(query.OrderIds, cancellationToken);

        return orders
            .Where(o => o.OrderStatus == "Confirmed")
            .Select(o => GetIropsOrdersHandler.ProjectToIropsDto(o, query.FlightNumber, query.DepartureDate))
            .Where(dto => dto is not null)
            .ToList();
    }
}
