using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.ReissueTickets;

public sealed class ReissueTicketsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<ReissueTicketsHandler> _logger;

    public ReissueTicketsHandler(ITicketRepository ticketRepository, ILogger<ReissueTicketsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<ReissueTicketsResponse> HandleAsync(ReissueTicketsRequest request, CancellationToken cancellationToken = default)
    {
        // Void old tickets
        foreach (var eTicketNumber in request.VoidedETicketNumbers)
        {
            var oldTicket = await _ticketRepository.GetByETicketNumberAsync(eTicketNumber, cancellationToken);
            if (oldTicket is not null && !oldTicket.IsVoided)
            {
                oldTicket.Void();
                await _ticketRepository.UpdateAsync(oldTicket, cancellationToken);
                _logger.LogInformation("Voided ticket {ETicketNumber} for reissuance", eTicketNumber);
            }
        }

        // Issue new tickets
        var baseSequence = await _ticketRepository.GetTicketCountAsync(cancellationToken);
        var ticketSummaries = new List<TicketSummary>();
        var sequence = baseSequence;

        foreach (var segment in request.Segments)
        {
            foreach (var passenger in request.Passengers)
            {
                sequence++;
                var eTicketNumber = $"932-{sequence:D10}";

                var seatAssignment = segment.SeatAssignments?
                    .FirstOrDefault(s => s.PassengerId == passenger.PassengerId);

                var ticketData = new
                {
                    seatAssignment = seatAssignment != null ? new
                    {
                        seatNumber = seatAssignment.SeatNumber,
                        positionType = seatAssignment.PositionType,
                        deckCode = seatAssignment.DeckCode
                    } : null,
                    ssrCodes = Array.Empty<object>(),
                    changeHistory = new[] { new { eventType = "Reissued", occurredAt = DateTime.UtcNow.ToString("o"), actor = request.Actor, detail = $"Reissued — reason: {request.Reason}" } }
                };

                var ticketDataJson = JsonSerializer.Serialize(ticketData, SharedJsonOptions.CamelCase);
                var departureDate = DateTime.Parse(segment.DepartureDate);

                var ticket = Ticket.Create(
                    eTicketNumber, segment.InventoryId, segment.FlightNumber,
                    departureDate, request.BookingReference,
                    passenger.PassengerId, passenger.GivenName, passenger.Surname,
                    segment.CabinCode, segment.FareBasisCode, ticketDataJson);

                await _ticketRepository.CreateAsync(ticket, cancellationToken);
                ticketSummaries.Add(DeliveryMapper.ToTicketSummary(ticket, segment.SegmentId));
            }
        }

        return new ReissueTicketsResponse
        {
            VoidedETicketNumbers = request.VoidedETicketNumbers,
            Tickets = ticketSummaries
        };
    }
}
