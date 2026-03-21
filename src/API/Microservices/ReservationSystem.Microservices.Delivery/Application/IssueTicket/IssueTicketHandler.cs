using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.IssueTicket;

/// <summary>
/// Handles the <see cref="IssueTicketCommand"/>.
/// Issues a new electronic ticket and associates it with a manifest.
/// </summary>
public sealed class IssueTicketHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<IssueTicketHandler> _logger;

    public IssueTicketHandler(
        ITicketRepository ticketRepository,
        IManifestRepository manifestRepository,
        ILogger<IssueTicketHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<Ticket> HandleAsync(
        IssueTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
