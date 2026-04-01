using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.VoidTicket;

public sealed class VoidTicketHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<VoidTicketHandler> _logger;

    public VoidTicketHandler(ITicketRepository ticketRepository, ILogger<VoidTicketHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<VoidTicketResponse?> HandleAsync(string eTicketNumber, VoidTicketRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByETicketNumberAsync(eTicketNumber, cancellationToken);

        if (ticket is null)
        {
            _logger.LogWarning("Void requested for unknown ticket {ETicketNumber}", eTicketNumber);
            return null;
        }

        if (ticket.IsVoided)
        {
            _logger.LogWarning("Ticket {ETicketNumber} is already voided", eTicketNumber);
            throw new InvalidOperationException($"Ticket '{eTicketNumber}' is already voided.");
        }

        ticket.Void();
        await _ticketRepository.UpdateAsync(ticket, cancellationToken);

        _logger.LogInformation("Voided ticket {ETicketNumber} — reason: {Reason}, actor: {Actor}",
            eTicketNumber, request.Reason, request.Actor);

        return new VoidTicketResponse
        {
            ETicketNumber = ticket.ETicketNumber,
            IsVoided = true,
            VoidedAt = ticket.VoidedAt
        };
    }
}
