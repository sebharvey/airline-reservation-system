using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.OciBoardingDocs;

public sealed record OciBoardingDocsCommand(string DepartureAirport, IReadOnlyList<string> TicketNumbers);

public sealed record BoardingCard(
    string TicketNumber,
    string PassengerId,
    string GivenName,
    string Surname,
    string FlightNumber,
    string DepartureDate,
    string SeatNumber,
    string CabinCode,
    string SequenceNumber,
    string Origin,
    string Destination,
    string BcbpString);

public sealed record OciBoardingDocsResult(IReadOnlyList<BoardingCard> BoardingCards);

public sealed class OciBoardingDocsHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<OciBoardingDocsHandler> _logger;

    public OciBoardingDocsHandler(
        IManifestRepository manifestRepository,
        ILogger<OciBoardingDocsHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<OciBoardingDocsResult> HandleAsync(OciBoardingDocsCommand command, CancellationToken cancellationToken = default)
    {
        var boardingCards = new List<BoardingCard>();

        for (var sequenceIndex = 0; sequenceIndex < command.TicketNumbers.Count; sequenceIndex++)
        {
            var ticketNumber = command.TicketNumbers[sequenceIndex];
            var manifests = await _manifestRepository.GetByETicketNumberAsync(ticketNumber, cancellationToken);

            // Only issue boarding cards for checked-in segments departing from the requested airport
            var eligibleManifests = manifests
                .Where(m => string.Equals(m.Origin, command.DepartureAirport, StringComparison.OrdinalIgnoreCase)
                         && m.CheckedIn)
                .OrderBy(m => m.DepartureDate)
                .ToList();

            foreach (var manifest in eligibleManifests)
            {
                var sequenceNumber = (sequenceIndex + 1).ToString("D4");
                var bcbp = BuildBcbpString(manifest, sequenceNumber);

                boardingCards.Add(new BoardingCard(
                    TicketNumber: manifest.ETicketNumber,
                    PassengerId: manifest.PassengerId,
                    GivenName: manifest.GivenName,
                    Surname: manifest.Surname,
                    FlightNumber: manifest.FlightNumber,
                    DepartureDate: manifest.DepartureDate.ToString("yyyy-MM-dd"),
                    SeatNumber: manifest.SeatNumber,
                    CabinCode: manifest.CabinCode,
                    SequenceNumber: sequenceNumber,
                    Origin: manifest.Origin,
                    Destination: manifest.Destination,
                    BcbpString: bcbp));

                _logger.LogInformation(
                    "Generated boarding card for ticket {TicketNumber} on {FlightNumber} {Origin}-{Destination}",
                    ticketNumber, manifest.FlightNumber, manifest.Origin, manifest.Destination);
            }
        }

        return new OciBoardingDocsResult(boardingCards);
    }

    /// <summary>
    /// Builds an IATA Resolution 792 BCBP string for a single manifest (leg).
    /// Format: M1{name}{bookingRef}{origin}{destination}{carrier}{flightNum}{julianDate}{cabin}{seat}{seq}{status}
    /// </summary>
    private static string BuildBcbpString(Manifest manifest, string sequenceNumber)
    {
        // Passenger name: SURNAME/GIVENNAME padded to 20 chars
        var rawName = $"{manifest.Surname.ToUpperInvariant()}/{manifest.GivenName.ToUpperInvariant()}";
        var name = rawName.Length >= 20 ? rawName[..20] : rawName.PadRight(20);

        // Booking reference: E + 6-char PNR padded to 7 chars
        var pnr = manifest.BookingReference.ToUpperInvariant().PadRight(6)[..6];
        var bookingRef = $"E{pnr}".PadRight(7)[..7];

        // Origin/Destination/Carrier: each fixed width
        var origin = manifest.Origin.ToUpperInvariant().PadRight(3)[..3];
        var destination = manifest.Destination.ToUpperInvariant().PadRight(3)[..3];

        // Extract carrier code from flight number (letters before digits)
        var carrier = ExtractCarrierCode(manifest.FlightNumber).PadRight(2)[..2];

        // Flight number: numeric part padded to 4 chars
        var flightNum = ExtractFlightNum(manifest.FlightNumber).PadLeft(4, '0')[..4];

        // Julian date (day of year)
        var julianDate = manifest.DepartureDate.DayOfYear.ToString("D3");

        // Cabin code: single char
        var cabin = manifest.CabinCode.Length > 0 ? manifest.CabinCode[..1].ToUpperInvariant() : "Y";

        // Seat: padded to 4 chars (e.g. "001A"), use "0000" if unassigned
        var seatRaw = string.IsNullOrWhiteSpace(manifest.SeatNumber) ? "0000" : manifest.SeatNumber.PadLeft(4, '0');
        var seat = seatRaw.Length >= 4 ? seatRaw[..4] : seatRaw.PadLeft(4, '0');

        // Sequence number: 4 digits
        var seq = sequenceNumber.Length >= 4 ? sequenceNumber[..4] : sequenceNumber.PadLeft(4, '0');

        // Passenger status: 1 = checked in
        const string passengerStatus = "1";

        // Mandatory section
        var mandatory = $"M1{name}{bookingRef}{origin}{destination}{carrier}{flightNum}{julianDate}{cabin}{seat}{seq}{passengerStatus}";

        return mandatory;
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i > 0 ? flightNumber[..i] : "AX";
    }

    private static string ExtractFlightNum(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i < flightNumber.Length ? flightNumber[i..] : "0000";
    }
}
