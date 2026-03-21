using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.IssueTickets;

public sealed class IssueTicketsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<IssueTicketsHandler> _logger;

    public IssueTicketsHandler(ITicketRepository ticketRepository, ILogger<IssueTicketsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<IssueTicketsResponse> HandleAsync(IssueTicketsRequest request, CancellationToken cancellationToken = default)
    {
        var baseSequence = await _ticketRepository.GetTicketCountAsync(cancellationToken);
        var ticketSummaries = new List<TicketSummary>();
        var sequence = baseSequence;

        foreach (var segment in request.Segments)
        {
            foreach (var passenger in request.Passengers)
            {
                sequence++;
                var eTicketNumber = $"932-{sequence:D10}";

                // Build TicketData JSON
                var seatAssignment = segment.SeatAssignments?
                    .FirstOrDefault(s => s.PassengerId == passenger.PassengerId);

                var ssrCodes = segment.SsrCodes?
                    .Where(s => s.PassengerId == passenger.PassengerId)
                    .ToList() ?? [];

                var ticketData = new
                {
                    seatAssignment = seatAssignment != null ? new
                    {
                        seatNumber = seatAssignment.SeatNumber,
                        positionType = seatAssignment.PositionType,
                        deckCode = seatAssignment.DeckCode
                    } : null,
                    ssrCodes = ssrCodes.Select(s => new { code = s.Code, description = s.Description, segmentRef = s.SegmentRef }),
                    changeHistory = new[] { new { eventType = "Issued", occurredAt = DateTime.UtcNow.ToString("o"), actor = "RetailAPI", detail = "Initial ticket issuance" } }
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

                _logger.LogInformation("Issued ticket {ETicketNumber} for {PassengerId} on {FlightNumber}",
                    eTicketNumber, passenger.PassengerId, segment.FlightNumber);
            }
        }

        return new IssueTicketsResponse { Tickets = ticketSummaries };
    }
}
