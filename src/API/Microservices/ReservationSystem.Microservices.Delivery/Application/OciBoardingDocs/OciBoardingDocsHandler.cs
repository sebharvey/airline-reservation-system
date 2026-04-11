using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;

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
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<OciBoardingDocsHandler> _logger;

    public OciBoardingDocsHandler(
        ITicketRepository ticketRepository,
        ILogger<OciBoardingDocsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<OciBoardingDocsResult> HandleAsync(OciBoardingDocsCommand command, CancellationToken cancellationToken = default)
    {
        var boardingCards = new List<BoardingCard>();
        var sequenceIndex = 0;

        foreach (var ticketNumber in command.TicketNumbers)
        {
            var ticket = await _ticketRepository.GetByETicketNumberAsync(ticketNumber, cancellationToken);
            if (ticket is null)
            {
                _logger.LogWarning("Ticket {TicketNumber} not found for boarding docs", ticketNumber);
                continue;
            }

            var (givenName, surname) = ticket.GetPassengerName();
            var checkedInCoupons = ticket.GetCheckedInCouponsForOrigin(command.DepartureAirport);

            foreach (var coupon in checkedInCoupons)
            {
                sequenceIndex++;
                var sequenceNumber = sequenceIndex.ToString("D4");
                var bcbp = BuildBcbpString(ticket.BookingReference, surname, givenName, coupon, sequenceNumber);

                boardingCards.Add(new BoardingCard(
                    TicketNumber: DeliveryMapper.FormatETicketNumber(ticket.TicketNumber),
                    PassengerId: ticket.PassengerId,
                    GivenName: givenName,
                    Surname: surname,
                    FlightNumber: coupon.FlightNumber,
                    DepartureDate: coupon.DepartureDate,
                    SeatNumber: coupon.SeatNumber ?? "",
                    CabinCode: coupon.ClassOfService,
                    SequenceNumber: sequenceNumber,
                    Origin: coupon.Origin,
                    Destination: coupon.Destination,
                    BcbpString: bcbp));

                _logger.LogInformation(
                    "Generated boarding card for ticket {TicketNumber} on {FlightNumber} {Origin}-{Destination}",
                    ticketNumber, coupon.FlightNumber, coupon.Origin, coupon.Destination);
            }
        }

        return new OciBoardingDocsResult(boardingCards);
    }

    /// <summary>
    /// Builds an IATA Resolution 792 BCBP string for a single coupon (leg).
    /// Format: M1{name}{bookingRef}{origin}{destination}{carrier}{flightNum}{julianDate}{cabin}{seat}{seq}{status}
    /// </summary>
    private static string BuildBcbpString(
        string bookingReference,
        string surname,
        string givenName,
        Domain.Entities.CouponInfo coupon,
        string sequenceNumber)
    {
        // Passenger name: SURNAME/GIVENNAME padded to 20 chars
        var rawName = $"{surname.ToUpperInvariant()}/{givenName.ToUpperInvariant()}";
        var name = rawName.Length >= 20 ? rawName[..20] : rawName.PadRight(20);

        // Booking reference: E + 6-char PNR padded to 7 chars
        var pnr = bookingReference.ToUpperInvariant().PadRight(6)[..6];
        var bookingRef = $"E{pnr}".PadRight(7)[..7];

        // Origin/Destination: each 3 chars
        var origin = coupon.Origin.ToUpperInvariant().PadRight(3)[..3];
        var destination = coupon.Destination.ToUpperInvariant().PadRight(3)[..3];

        // Extract carrier code from flight number (letters before digits)
        var carrier = ExtractCarrierCode(coupon.FlightNumber).PadRight(2)[..2];

        // Flight number: numeric part padded to 4 chars
        var flightNum = ExtractFlightNum(coupon.FlightNumber).PadLeft(4, '0')[..4];

        // Julian date (day of year) from departure date string
        var julianDate = "001";
        if (DateTime.TryParse(coupon.DepartureDate, out var depDate))
            julianDate = depDate.DayOfYear.ToString("D3");

        // Cabin code: single char
        var cabin = coupon.ClassOfService.Length > 0 ? coupon.ClassOfService[..1].ToUpperInvariant() : "Y";

        // Seat: padded to 4 chars, "0000" if unassigned
        var seatRaw = string.IsNullOrWhiteSpace(coupon.SeatNumber) ? "0000" : coupon.SeatNumber.PadLeft(4, '0');
        var seat = seatRaw.Length >= 4 ? seatRaw[..4] : seatRaw.PadLeft(4, '0');

        // Sequence number: 4 digits
        var seq = sequenceNumber.Length >= 4 ? sequenceNumber[..4] : sequenceNumber.PadLeft(4, '0');

        // Passenger status: 1 = checked in
        const string passengerStatus = "1";

        return $"M1{name}{bookingRef}{origin}{destination}{carrier}{flightNum}{julianDate}{cabin}{seat}{seq}{passengerStatus}";
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
