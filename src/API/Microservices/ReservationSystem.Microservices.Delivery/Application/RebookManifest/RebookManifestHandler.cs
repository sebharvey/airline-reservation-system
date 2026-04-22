using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.RebookManifest;

public sealed record RebookManifestCommand(
    string BookingReference,
    string FromFlightNumber,
    DateOnly FromDepartureDate,
    Guid ToInventoryId,
    string ToFlightNumber,
    string ToOrigin,
    string ToDestination,
    DateOnly ToDepartureDate,
    TimeOnly ToDepartureTime,
    TimeOnly ToArrivalTime,
    string ToCabinCode,
    IReadOnlyList<(string PassengerId, string ETicketNumber)> Passengers);

public sealed class RebookManifestHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<RebookManifestHandler> _logger;

    public RebookManifestHandler(
        ITicketRepository ticketRepository,
        IManifestRepository manifestRepository,
        ILogger<RebookManifestHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(RebookManifestCommand command, CancellationToken ct = default)
    {
        var tickets = await _ticketRepository.GetByBookingReferenceAsync(command.BookingReference, ct);

        var passengerRebooks = new Dictionary<string, ManifestPassengerRebook>(StringComparer.OrdinalIgnoreCase);

        foreach (var (passengerId, eTicketNumber) in command.Passengers)
        {
            var ticket = tickets.LastOrDefault(t =>
                string.Equals(t.PassengerId, passengerId, StringComparison.OrdinalIgnoreCase)
                && !t.IsVoided);

            if (ticket is null)
            {
                _logger.LogWarning(
                    "No active ticket found for passenger {PassengerId} in booking {BookingRef} — manifest entry will not be rebooked",
                    passengerId, command.BookingReference);
                continue;
            }

            passengerRebooks[passengerId] = new ManifestPassengerRebook(ticket.TicketId, eTicketNumber);
        }

        if (passengerRebooks.Count == 0)
        {
            _logger.LogWarning("No passengers resolved for manifest rebook on booking {BookingRef}", command.BookingReference);
            return 0;
        }

        return await _manifestRepository.RebookByBookingAndFlightAsync(
            command.BookingReference,
            command.FromFlightNumber,
            command.FromDepartureDate,
            command.ToInventoryId,
            command.ToFlightNumber,
            command.ToOrigin,
            command.ToDestination,
            command.ToDepartureDate,
            command.ToDepartureTime,
            command.ToArrivalTime,
            command.ToCabinCode,
            passengerRebooks,
            ct);
    }
}
