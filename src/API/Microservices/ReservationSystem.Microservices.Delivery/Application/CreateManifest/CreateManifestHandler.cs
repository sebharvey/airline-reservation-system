using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.CreateManifest;

public sealed record ManifestEntryRequest(
    string TicketId,
    string InventoryId,
    string FlightNumber,
    string Origin,
    string Destination,
    string DepartureDate,
    string ETicketNumber,
    string PassengerId,
    string GivenName,
    string Surname,
    string CabinCode,
    string? SeatNumber);

public sealed record CreateManifestCommand(
    string BookingReference,
    IReadOnlyList<ManifestEntryRequest> Entries);

public sealed class CreateManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<CreateManifestHandler> _logger;

    public CreateManifestHandler(IManifestRepository manifestRepository, ILogger<CreateManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task HandleAsync(CreateManifestCommand command, CancellationToken cancellationToken = default)
    {
        foreach (var entry in command.Entries)
        {
            if (!Guid.TryParse(entry.TicketId, out var ticketId)) continue;
            if (!Guid.TryParse(entry.InventoryId, out var inventoryId)) continue;

            // Skip if manifest already exists for this passenger/flight combination
            var existing = await _manifestRepository.GetByInventoryAndPassengerAsync(inventoryId, entry.PassengerId, cancellationToken);
            if (existing is not null)
            {
                _logger.LogDebug("Manifest already exists for inventory {InventoryId} pax {PassengerId} — skipping", inventoryId, entry.PassengerId);
                continue;
            }

            if (!DateTime.TryParse(entry.DepartureDate, out var departureDate))
                departureDate = DateTime.UtcNow.Date;

            // Use seat number from entry if available; fall back to last 5 chars of eTicketNumber
            // to satisfy the unique (InventoryId, SeatNumber) constraint when no seat is assigned yet
            var seatNumber = !string.IsNullOrWhiteSpace(entry.SeatNumber)
                ? entry.SeatNumber
                : entry.ETicketNumber.Length >= 5 ? entry.ETicketNumber[^5..] : entry.ETicketNumber;

            var manifest = Manifest.Create(
                ticketId: ticketId,
                inventoryId: inventoryId,
                flightNumber: entry.FlightNumber,
                origin: entry.Origin,
                destination: entry.Destination,
                departureDate: departureDate,
                aircraftType: "UNKN",
                seatNumber: seatNumber,
                cabinCode: entry.CabinCode,
                bookingReference: command.BookingReference,
                eTicketNumber: entry.ETicketNumber,
                passengerId: entry.PassengerId,
                givenName: entry.GivenName,
                surname: entry.Surname,
                ssrCodes: null,
                departureTime: TimeSpan.Zero,
                arrivalTime: TimeSpan.Zero);

            await _manifestRepository.CreateAsync(manifest, cancellationToken);

            _logger.LogInformation(
                "Created manifest {ManifestId} for ticket {ETicketNumber} on {FlightNumber} {Origin}-{Destination}",
                manifest.ManifestId, entry.ETicketNumber, entry.FlightNumber, entry.Origin, entry.Destination);
        }
    }
}
