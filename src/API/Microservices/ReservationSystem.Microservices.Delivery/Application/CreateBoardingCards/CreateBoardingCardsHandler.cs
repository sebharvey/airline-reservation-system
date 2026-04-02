using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;

public sealed class CreateBoardingCardsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<CreateBoardingCardsHandler> _logger;

    public CreateBoardingCardsHandler(
        ITicketRepository ticketRepository,
        IManifestRepository manifestRepository,
        ILogger<CreateBoardingCardsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<CreateBoardingCardsResponse> HandleAsync(
        CreateBoardingCardsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _ticketRepository.GetByBookingReferenceAsync(
            request.BookingReference, cancellationToken);

        var boardingCards = new List<BoardingCardResponse>();
        var sequenceCounter = 0;

        foreach (var passengerRequest in request.Passengers)
        {
            var ticket = tickets.FirstOrDefault(t =>
                t.PassengerId == passengerRequest.PassengerId && !t.IsVoided);

            if (ticket is null)
            {
                _logger.LogWarning(
                    "No valid ticket found for passenger {PassengerId} on booking {BookingRef}",
                    passengerRequest.PassengerId, request.BookingReference);
                continue;
            }

            JsonDocument? ticketDoc = null;
            try { ticketDoc = JsonDocument.Parse(ticket.TicketData); }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse TicketData for ticket {ETicketNumber}", ticket.ETicketNumber);
                continue;
            }

            using (ticketDoc)
            {
                var root = ticketDoc.RootElement;

                var givenName = root.TryGetProperty("passenger", out var pax) &&
                                pax.TryGetProperty("givenName", out var gn)
                    ? gn.GetString() ?? string.Empty : string.Empty;
                var surname = root.TryGetProperty("passenger", out var pax2) &&
                              pax2.TryGetProperty("surname", out var sn)
                    ? sn.GetString() ?? string.Empty : string.Empty;

                root.TryGetProperty("coupons", out var couponsEl);

                foreach (var inventoryIdStr in passengerRequest.InventoryIds)
                {
                    if (!Guid.TryParse(inventoryIdStr, out var inventoryId))
                        continue;

                    var manifest = await _manifestRepository.GetByInventoryAndPassengerAsync(
                        inventoryId, passengerRequest.PassengerId, cancellationToken);

                    if (manifest is null)
                    {
                        _logger.LogWarning(
                            "Manifest not found for inventory {InventoryId} / passenger {PassengerId}",
                            inventoryId, passengerRequest.PassengerId);
                        continue;
                    }

                    // Mark the manifest entry as checked in
                    manifest.UpdateCheckIn(true, DateTime.UtcNow);
                    await _manifestRepository.UpdateAsync(manifest, cancellationToken);

                    // Find the matching coupon from ticket data by flight number
                    JsonElement? coupon = FindCoupon(couponsEl, manifest.FlightNumber, manifest.Origin, manifest.Destination);

                    var seatNumber = coupon.HasValue && coupon.Value.TryGetProperty("seat", out var seatEl)
                                     && seatEl.ValueKind != JsonValueKind.Null
                        ? seatEl.GetString() ?? manifest.SeatNumber
                        : manifest.SeatNumber;
                    if (string.IsNullOrWhiteSpace(seatNumber)) seatNumber = "TBA";

                    var classOfService = coupon.HasValue && coupon.Value.TryGetProperty("classOfService", out var cosEl)
                        ? cosEl.GetString() ?? manifest.CabinCode
                        : manifest.CabinCode;

                    var departureDate = manifest.DepartureDate.ToString("yyyy-MM-dd");
                    var departureTime = manifest.DepartureTime.ToString(@"hh\:mm");
                    var departureDateTime = $"{departureDate}T{departureTime}:00Z";

                    sequenceCounter++;
                    var sequenceNumber = sequenceCounter.ToString("D4");

                    var bcbp = BuildBcbp(
                        surname, givenName, request.BookingReference,
                        manifest.FlightNumber, manifest.Origin, manifest.Destination,
                        seatNumber, sequenceNumber);

                    // Boarding time is 30 minutes before departure
                    var boardingTime = DateTime.TryParse(departureDateTime, out var dep)
                        ? dep.AddMinutes(-30).ToString("o")
                        : departureDateTime;

                    boardingCards.Add(new BoardingCardResponse
                    {
                        BookingReference = request.BookingReference,
                        PassengerId = passengerRequest.PassengerId,
                        GivenName = givenName,
                        Surname = surname,
                        FlightNumber = manifest.FlightNumber,
                        DepartureDateTime = departureDateTime,
                        SeatNumber = seatNumber,
                        CabinCode = classOfService,
                        SequenceNumber = sequenceNumber,
                        BcbpString = bcbp,
                        Origin = manifest.Origin,
                        Destination = manifest.Destination,
                        ETicketNumber = ticket.ETicketNumber,
                        Gate = "TBC",
                        BoardingTime = boardingTime
                    });

                    _logger.LogInformation(
                        "Checked in passenger {PassengerId} on flight {FlightNumber} (booking {BookingRef})",
                        passengerRequest.PassengerId, manifest.FlightNumber, request.BookingReference);
                }
            }
        }

        return new CreateBoardingCardsResponse { BoardingCards = boardingCards };
    }

    private static JsonElement? FindCoupon(JsonElement couponsEl, string flightNumber, string origin, string destination)
    {
        if (couponsEl.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var coupon in couponsEl.EnumerateArray())
        {
            var couponFlight = coupon.TryGetProperty("marketing", out var mkt) &&
                               mkt.TryGetProperty("flightNumber", out var fn)
                ? fn.GetString() : null;

            var couponOrigin = coupon.TryGetProperty("origin", out var o) ? o.GetString() : null;
            var couponDest = coupon.TryGetProperty("destination", out var d) ? d.GetString() : null;

            if (string.Equals(couponFlight, flightNumber, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(couponOrigin, origin, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(couponDest, destination, StringComparison.OrdinalIgnoreCase)))
            {
                return coupon;
            }
        }

        return null;
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
        var fn = flightNumber.Replace(carrier, string.Empty).PadStart(4, '0');
        var seat = seatNumber.PadStart(4, '0');

        return $"M1{name}E{bookingRef} {origin}{destination}{carrier} {fn} {DateTime.UtcNow:yyyy-MM-dd}J{seat}{sequenceNumber}1";
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i > 0 ? flightNumber[..i] : flightNumber;
    }
}
