using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetOrderByETicket;

/// <summary>
/// Handles the <see cref="GetOrderByETicketQuery"/>.
/// Retrieves an order by e-ticket number.
/// </summary>
public sealed class GetOrderByETicketHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetOrderByETicketHandler> _logger;

    public GetOrderByETicketHandler(
        IOrderRepository repository,
        ILogger<GetOrderByETicketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        GetOrderByETicketQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving order by e-ticket number {ETicketNumber}", query.ETicketNumber);

        var order = await _repository.GetByETicketNumberAsync(query.ETicketNumber, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order with e-ticket number {ETicketNumber} not found", query.ETicketNumber);
        }

        return order;
    }
}
