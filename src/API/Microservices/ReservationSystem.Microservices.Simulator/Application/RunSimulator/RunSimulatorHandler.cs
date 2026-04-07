using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Simulator.Domain.ExternalServices;
using ReservationSystem.Microservices.Simulator.Models;

namespace ReservationSystem.Microservices.Simulator.Application.RunSimulator;

/// <summary>
/// Creates 5 confirmed orders for the next day's AX001 (LHR → JFK) flight,
/// each with a random passenger count (1–6). Intended to be invoked by a
/// scheduled timer trigger every 60 minutes.
/// </summary>
internal sealed class RunSimulatorHandler
{
    private const int OrderCount  = 5;
    private const int MinPax      = 1;
    private const int MaxPax      = 6;
    private const string Origin      = "LHR";
    private const string Destination = "JFK";
    private const string FlightNumber = "AX001";

    private static readonly string[] FirstNames =
    [
        "James", "Oliver", "Harry", "George", "Noah", "Jack", "Charlie", "Jacob",
        "Amelia", "Olivia", "Isla", "Emily", "Poppy", "Ava", "Isabella", "Jessica",
        "Lily", "Sophie", "Liam", "Emma", "William", "Sophia", "Mason", "Charlotte"
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Jones", "Williams", "Taylor", "Brown", "Davies", "Evans", "Wilson",
        "Thomas", "Roberts", "Johnson", "Lewis", "Walker", "Robinson", "Wood", "Hall",
        "Green", "Clark", "Hughes", "Martin", "Scott", "White", "Harris", "Turner"
    ];

    private readonly IRetailApiClient _retailApiClient;
    private readonly ILogger<RunSimulatorHandler> _logger;

    public RunSimulatorHandler(
        IRetailApiClient retailApiClient,
        ILogger<RunSimulatorHandler> logger)
    {
        _retailApiClient = retailApiClient;
        _logger          = logger;
    }

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var departureDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var created = 0;

        _logger.LogInformation(
            "Simulator: starting {Count} order run for {FlightNumber} on {DepartureDate}",
            OrderCount, FlightNumber, departureDate);

        for (var i = 0; i < OrderCount; i++)
        {
            var paxCount = Random.Shared.Next(MinPax, MaxPax + 1);
            try
            {
                var (orderId, bookingRef) = await CreateOrderAsync(departureDate, paxCount, ct);
                created++;
                _logger.LogInformation(
                    "Simulator: order {Index}/{Total} created — orderId={OrderId} bookingRef={BookingRef} paxCount={PaxCount}",
                    i + 1, OrderCount, orderId, bookingRef, paxCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Simulator: order {Index}/{Total} failed (paxCount={PaxCount})",
                    i + 1, OrderCount, paxCount);
            }
        }

        _logger.LogInformation(
            "Simulator: run complete — {Created}/{Total} orders created for {FlightNumber} on {DepartureDate}",
            created, OrderCount, FlightNumber, departureDate);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<(string OrderId, string BookingRef)> CreateOrderAsync(
        string departureDate, int paxCount, CancellationToken ct)
    {
        // Step 1: search for AX001 on the departure date
        var searchRequest = new SearchSliceRequest(Origin, Destination, departureDate, paxCount, "Revenue");
        var searchResponse = await _retailApiClient.SearchSliceAsync(searchRequest, ct);

        var ax001 = searchResponse.Flights.FirstOrDefault(f => f.FlightNumber == FlightNumber)
            ?? throw new InvalidOperationException($"{FlightNumber} not found in search results for {departureDate}.");

        var offerIds = ax001.Cabins
            .SelectMany(c => c.FareFamilies)
            .Select(ff => ff.Offer.OfferId)
            .ToList();

        if (offerIds.Count == 0)
            throw new InvalidOperationException($"No offers found for {FlightNumber} on {departureDate}.");

        var offerId = offerIds[Random.Shared.Next(offerIds.Count)];

        // Step 2: create basket
        var basketRequest = new CreateBasketRequest(
            [new BasketSegment(offerId, searchResponse.SessionId)],
            "WEB", "GBP", "Revenue");

        var basketResponse = await _retailApiClient.CreateBasketAsync(basketRequest, ct);
        var basketId = basketResponse.BasketId;

        // Step 3: add passengers
        var passengers = GeneratePassengers(paxCount);
        await _retailApiClient.AddPassengersAsync(basketId, passengers, ct);

        // Step 4: get basket to extract inventory details
        var basket = await _retailApiClient.GetBasketAsync(basketId, ct);
        var flightOffer = basket.BasketData.FlightOffers.FirstOrDefault()
            ?? throw new InvalidOperationException($"Basket {basketId} has no flight offers.");

        // Step 5: get seatmap for the booked cabin
        var seatmap = await _retailApiClient.GetSeatmapAsync(
            flightOffer.InventoryId, flightOffer.AircraftType,
            flightOffer.FlightNumber, flightOffer.CabinCode, ct);

        var availableSeats = seatmap.Cabins
            .SelectMany(c => c.Seats)
            .Where(s => string.Equals(s.Availability, "available", StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => Random.Shared.Next())
            .Take(paxCount)
            .ToList();

        if (availableSeats.Count < paxCount)
            throw new InvalidOperationException(
                $"Not enough available seats on {FlightNumber}: needed {paxCount}, found {availableSeats.Count}.");

        // Step 6: add seats — one per passenger
        var seatAssignments = availableSeats.Select((seat, index) => new SeatAssignment(
            PassengerId:   $"PAX-{index + 1}",
            SegmentId:     flightOffer.InventoryId,
            BasketItemRef: flightOffer.BasketItemId,
            SeatOfferId:   seat.SeatOfferId,
            SeatNumber:    seat.SeatNumber,
            SeatPosition:  seat.Position,
            CabinCode:     seat.CabinCode,
            Price:         seat.Price,
            Currency:      seat.Currency)).ToList();

        await _retailApiClient.AddSeatsAsync(basketId, seatAssignments, ct);

        // Step 7: confirm and pay with a Luhn-valid test card
        var primary = passengers[0];
        var confirmRequest = new ConfirmBasketRequest(
            new PaymentRequest(
                Method:         "CreditCard",
                CardNumber:     "4111111111111111",
                ExpiryDate:     "12/28",
                Cvv:            "737",
                CardholderName: $"{primary.GivenName} {primary.Surname}"),
            LoyaltyPointsToRedeem: null);

        var confirmResponse = await _retailApiClient.ConfirmBasketAsync(basketId, confirmRequest, ct);
        return (confirmResponse.OrderId, confirmResponse.BookingReference);
    }

    private static List<PassengerRequest> GeneratePassengers(int count)
    {
        var passengers = new List<PassengerRequest>(count);

        for (var i = 0; i < count; i++)
        {
            var given   = FirstNames[Random.Shared.Next(FirstNames.Length)];
            var surname = LastNames[Random.Shared.Next(LastNames.Length)];
            var dob     = DateTime.UtcNow
                              .AddYears(-Random.Shared.Next(25, 65))
                              .AddDays(-Random.Shared.Next(0, 365))
                              .ToString("yyyy-MM-dd");
            var gender  = Random.Shared.Next(2) == 0 ? "M" : "F";
            var email   = $"{given.ToLowerInvariant()}.{surname.ToLowerInvariant()}{Random.Shared.Next(100, 999)}@simulator.apexair.com";
            var phone   = $"+447{Random.Shared.Next(100_000_000, 999_999_999)}";

            passengers.Add(new PassengerRequest(
                PassengerId:    $"PAX-{i + 1}",
                Type:           "ADT",
                GivenName:      given,
                Surname:        surname,
                DateOfBirth:    dob,
                Gender:         gender,
                LoyaltyNumber:  null,
                Contacts:       new PassengerContacts(email, phone),
                TravelDocument: null));
        }

        return passengers;
    }
}
