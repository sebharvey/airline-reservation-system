using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.WriteManifest;

public sealed class WriteManifestHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<WriteManifestHandler> _logger;

    public WriteManifestHandler(
        ITicketRepository ticketRepository,
        IManifestRepository manifestRepository,
        ILogger<WriteManifestHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<WriteManifestResponse> HandleAsync(
        WriteManifestRequest request, CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(request.DepartureDate, out var departureDate))
        {
            _logger.LogWarning("Invalid departureDate '{DepartureDate}' for booking {BookingRef}",
                request.DepartureDate, request.BookingReference);
            return new WriteManifestResponse { Written = 0, Skipped = request.Entries.Count };
        }

        TimeOnly.TryParse(request.DepartureTime, out var departureTime);
        TimeOnly.TryParse(request.ArrivalTime, out var arrivalTime);

        // Load all tickets for this booking once to avoid N+1 lookups
        var tickets = await _ticketRepository.GetByBookingReferenceAsync(
            request.BookingReference, cancellationToken);

        var written = 0;
        var skipped = 0;

        foreach (var entry in request.Entries)
        {
            // Skip if ticket issuance failed for this passenger
            if (string.IsNullOrWhiteSpace(entry.ETicketNumber))
            {
                skipped++;
                continue;
            }

            var ticket = tickets.FirstOrDefault(t =>
                string.Equals(t.PassengerId, entry.PassengerId, StringComparison.OrdinalIgnoreCase) &&
                !t.IsVoided);

            if (ticket is null)
            {
                _logger.LogWarning(
                    "No active ticket found for passenger {PassengerId} in booking {BookingRef}",
                    entry.PassengerId, request.BookingReference);
                skipped++;
                continue;
            }

            var manifest = Manifest.Create(
                ticketId:        ticket.TicketId,
                orderId:         request.OrderId,
                inventoryId:     request.InventoryId,
                flightNumber:    request.FlightNumber,
                origin:          request.Origin,
                destination:     request.Destination,
                departureDate:   departureDate,
                aircraftType:    request.AircraftType,
                seatNumber:      entry.SeatNumber,
                cabinCode:       entry.CabinCode,
                bookingReference: request.BookingReference,
                eTicketNumber:   entry.ETicketNumber,
                passengerId:     entry.PassengerId,
                givenName:       entry.GivenName,
                surname:         entry.Surname,
                departureTime:   departureTime,
                arrivalTime:     arrivalTime,
                bookingType:     request.BookingType);

            var inserted = await _manifestRepository.CreateAsync(manifest, cancellationToken);
            if (inserted) written++; else skipped++;
        }

        _logger.LogInformation(
            "Manifest write for {BookingRef}/{FlightNumber}: {Written} written, {Skipped} skipped",
            request.BookingReference, request.FlightNumber, written, skipped);

        return new WriteManifestResponse { Written = written, Skipped = skipped };
    }
}
