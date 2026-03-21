using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.ReissueTicket;

/// <summary>
/// Handles the <see cref="ReissueTicketCommand"/>.
/// Reissues an existing ticket with a new e-ticket number.
/// </summary>
public sealed class ReissueTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<ReissueTicketHandler> _logger;

    public ReissueTicketHandler(
        ITicketRepository repository,
        ILogger<ReissueTicketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Ticket?> HandleAsync(
        ReissueTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
