using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetBoardingCardsByBooking;

/// <summary>
/// Retrieves boarding cards for all checked-in passengers on a booking,
/// reconstructing BCBP data from the delivery.Ticket and delivery.Manifest tables.
/// This is a read-only operation — it does not mutate check-in state.
/// </summary>
public sealed class GetBoardingCardsByBookingHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<GetBoardingCardsByBookingHandler> _logger;

    public GetBoardingCardsByBookingHandler(
        ITicketRepository ticketRepository,
        IManifestRepository manifestRepository,
        ILogger<GetBoardingCardsByBookingHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<CreateBoardingCardsResponse> HandleAsync(
        string bookingReference,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _ticketRepository.GetByBookingReferenceAsync(bookingReference, cancellationToken);
        var ticketByETicket = tickets
            .Where(t => !t.IsVoided)
            .ToDictionary(t => t.ETicketNumber, StringComparer.OrdinalIgnoreCase);

        var checkedInManifests = await _manifestRepository.GetByBookingAsync(bookingReference, cancellationToken);

        var boardingCards = new List<BoardingCardResponse>();
        var sequenceCounter = 0;

        foreach (var manifest in checkedInManifests)
        {
            if (!ticketByETicket.TryGetValue(manifest.ETicketNumber, out var ticket))
            {
                _logger.LogWarning(
                    "No valid ticket found for ETicketNumber {ETicketNumber} on booking {BookingRef}",
                    manifest.ETicketNumber, bookingReference);
                continue;
            }

            var departureDate = manifest.DepartureDate.ToString("yyyy-MM-dd");
            var departureTime = manifest.DepartureTime.ToString(@"hh\:mm");
            var departureDateTime = $"{departureDate}T{departureTime}:00Z";

            sequenceCounter++;
            var sequenceNumber = sequenceCounter.ToString("D4");

            var bcbp = BuildBcbp(
                manifest.Surname, manifest.GivenName, bookingReference,
                manifest.FlightNumber, manifest.Origin, manifest.Destination,
                manifest.SeatNumber, sequenceNumber);

            var boardingTime = DateTime.TryParse(departureDateTime, out var dep)
                ? dep.AddMinutes(-30).ToString("o")
                : departureDateTime;

            boardingCards.Add(new BoardingCardResponse
            {
                BookingReference = bookingReference,
                PassengerId = manifest.PassengerId,
                GivenName = manifest.GivenName,
                Surname = manifest.Surname,
                FlightNumber = manifest.FlightNumber,
                DepartureDateTime = departureDateTime,
                SeatNumber = string.IsNullOrWhiteSpace(manifest.SeatNumber) ? "TBA" : manifest.SeatNumber,
                CabinCode = manifest.CabinCode,
                SequenceNumber = sequenceNumber,
                BcbpString = bcbp,
                Origin = manifest.Origin,
                Destination = manifest.Destination,
                ETicketNumber = ticket.ETicketNumber,
                Gate = "TBC",
                BoardingTime = boardingTime
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} boarding card(s) for booking {BookingRef}",
            boardingCards.Count, bookingReference);

        return new CreateBoardingCardsResponse { BoardingCards = boardingCards };
    }

    private static string BuildBcbp(
        string surname, string givenName, string bookingRef,
        string flightNumber, string origin, string destination,
        string seatNumber, string sequenceNumber)
    {
        var name = $"{surname}/{givenName}".Length > 20
            ? $"{surname}/{givenName}"[..20]
            : $"{surname}/{givenName}".PadRight(20);

        var carrier = ExtractCarrierCode(flightNumber);
        var fn = flightNumber.Replace(carrier, string.Empty).PadLeft(4, '0');
        var seat = seatNumber.PadLeft(4, '0');

        return $"M1{name}E{bookingRef} {origin}{destination}{carrier} {fn} {DateTime.UtcNow:yyyy-MM-dd}J{seat}{sequenceNumber}1";
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i > 0 ? flightNumber[..i] : flightNumber;
    }
}
