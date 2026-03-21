using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetTicket;

/// <summary>
/// Handles the <see cref="GetTicketQuery"/>.
/// Retrieves a ticket by its identifier.
/// </summary>
public sealed class GetTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<GetTicketHandler> _logger;

    public GetTicketHandler(
        ITicketRepository repository,
        ILogger<GetTicketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Ticket?> HandleAsync(
        GetTicketQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
