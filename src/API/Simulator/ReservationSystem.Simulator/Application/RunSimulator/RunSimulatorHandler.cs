using System.Globalization;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.RunSimulator;

/// <summary>
/// Creates 1–6 confirmed orders per run across random routes over the next 48 hours.
/// Most bookings are return trips. Cabin selection favours Economy, then Premium Economy,
/// then Business. SSRs are added on roughly a third of bookings.
/// Intended to be invoked by a scheduled timer trigger every 20 minutes.
/// </summary>
internal sealed class RunSimulatorHandler
{
    // ── Configuration ──────────────────────────────────────────────────────────

    private const int MinOrders = 5;
    private const int MaxOrders = 10;
    private const int MinPax    = 1;
    private const int MaxPax    = 6;

    /// <summary>Probability (0–100) that a booking includes a return flight.</summary>
    private const int ReturnProbabilityPct = 70;

    /// <summary>Probability (0–100) that a booking includes SSR selections.</summary>
    private const int SsrProbabilityPct = 35;

    // ── Route catalogue — all daily direct routes ──────────────────────────────

    /// <summary>Outbound leg origin/destination pairs. Return uses the reverse.</summary>
    private static readonly (string Origin, string Destination)[] Routes =
    [
        ("LHR", "JFK"),
        ("LHR", "LAX"),
        ("LHR", "MIA"),
        ("LHR", "SFO"),
        ("LHR", "ORD"),
        ("LHR", "HKG"),
        ("LHR", "NRT"),
    ];

    // ── Cabin preference weights ────────────────────────────────────────────────

    // Economy (Y) is sold first, then Premium Economy (W), then Business (J).
    private static readonly (string Code, int Weight)[] CabinWeights =
    [
        ("Y", 60),   // Economy
        ("W", 25),   // Premium Economy
        ("J", 15),   // Business
    ];

    // ── SSR catalogue — realistic codes used in the simulation ─────────────────

    private static readonly string[] SsrCodes =
    [
        "VGML",  // Vegetarian meal
        "HNML",  // Hindu meal
        "MOML",  // Muslim / halal meal
        "KSML",  // Kosher meal
        "DBML",  // Diabetic meal
        "GFML",  // Gluten-free meal
        "WCHR",  // Wheelchair — can walk, needs assistance over distances
        "MAAS",  // Meet and assist
    ];

    // ── Name pools ─────────────────────────────────────────────────────────────

    private static readonly string[] FirstNames =
    [
        // British / Western
        "James", "Oliver", "Harry", "Noah", "Jack", "Charlie", "William", "Liam",
        "Thomas", "Samuel", "Max", "Daniel", "Ethan", "Lucas", "Alexander",
        "Amelia", "Olivia", "Isla", "Emily", "Poppy", "Ava", "Isabella",
        "Lily", "Sophie", "Emma", "Charlotte", "Grace", "Sienna", "Ellie",
        // South Asian
        "Aryan", "Rohan", "Vikram", "Amir", "Ravi", "Priya", "Anjali", "Fatima", "Aisha",
        // East Asian
        "Wei", "Chen", "Kai", "Mei", "Yuki", "Hina",
        // African
        "Kwame", "Oluwaseun", "Chioma", "Amara",
        // Other international
        "Nadia", "Zara", "Aaliyah", "Sofia", "Elena",
    ];

    private static readonly string[] LastNames =
    [
        // British
        "Smith", "Jones", "Williams", "Taylor", "Brown", "Davies", "Evans",
        "Wilson", "Thomas", "Roberts", "Johnson", "Lewis", "Walker", "Robinson",
        "Wood", "Hall", "Green", "Clark", "Hughes", "Martin", "Scott", "White",
        // South Asian
        "Khan", "Patel", "Singh", "Ahmed", "Sharma", "Chaudhary",
        // East Asian
        "Zhang", "Liu", "Chen", "Nakamura", "Tanaka", "Park",
        // African
        "Okafor", "Adeyemi", "Mensah", "Diallo",
        // Other international
        "Müller", "Rossi", "Fernandez", "Bergström", "Kowalski",
    ];

    // ── Dependencies ───────────────────────────────────────────────────────────

    private readonly IRetailApiClient _retailApiClient;
    private readonly ILogger<RunSimulatorHandler> _logger;

    public RunSimulatorHandler(
        IRetailApiClient retailApiClient,
        ILogger<RunSimulatorHandler> logger)
    {
        _retailApiClient = retailApiClient;
        _logger          = logger;
    }

    // ── Entry point ────────────────────────────────────────────────────────────

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var orderCount = Random.Shared.Next(MinOrders, MaxOrders + 1);
        var created    = 0;

        _logger.LogInformation("Simulator: starting run — targeting {Count} orders", orderCount);

        for (var i = 0; i < orderCount; i++)
        {
            var paxCount = Random.Shared.Next(MinPax, MaxPax + 1);
            try
            {
                var (orderId, bookingRef, route, isReturn) = await CreateOrderAsync(paxCount, ct);
                created++;
                _logger.LogInformation(
                    "Simulator: order {Index}/{Total} created — orderId={OrderId} ref={BookingRef} " +
                    "route={Route} return={IsReturn} pax={PaxCount}",
                    i + 1, orderCount, orderId, bookingRef, route, isReturn, paxCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Simulator: order {Index}/{Total} failed (pax={PaxCount}) — skipping to next",
                    i + 1, orderCount, paxCount);
            }
        }

        _logger.LogInformation(
            "Simulator: run complete — {Created}/{Total} orders created",
            created, orderCount);
    }

    // ── Order creation ─────────────────────────────────────────────────────────

    private async Task<(string OrderId, string BookingRef, string Route, bool IsReturn)> CreateOrderAsync(
        int paxCount, CancellationToken ct)
    {
        var now   = DateTime.UtcNow;
        var route = Routes[Random.Shared.Next(Routes.Length)];

        // ── Step 1: Search outbound ────────────────────────────────────────────
        // Pick randomly between today and tomorrow for the departure date.
        var departureDateOffset = Random.Shared.Next(0, 2); // 0 = today, 1 = tomorrow
        var outboundDate        = now.Date.AddDays(departureDateOffset).ToString("yyyy-MM-dd");

        var outboundSearchReq = new SearchSliceRequest(route.Origin, route.Destination, outboundDate, paxCount, "Revenue");
        var outboundSearchRes = await _retailApiClient.SearchSliceAsync(outboundSearchReq, ct);

        var outboundLeg = SelectValidLeg(outboundSearchRes, now)
            ?? throw new InvalidOperationException(
                $"No outbound flights within the valid window for {route.Origin}→{route.Destination} on {outboundDate}.");

        var outboundCabin = SelectCabin(outboundLeg.Cabins);
        var outboundOffer = outboundCabin.FareFamilies[Random.Shared.Next(outboundCabin.FareFamilies.Count)].Offer;

        // ── Step 2: Optionally search return ──────────────────────────────────
        var hasReturn     = Random.Shared.Next(100) < ReturnProbabilityPct;
        string? returnSessionId = null;
        string? returnOfferId   = null;

        if (hasReturn)
        {
            // Return 1–7 days after outbound departure
            var returnDayOffset = Random.Shared.Next(1, 8);
            var returnDate = DateTime.ParseExact(outboundDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                                     .AddDays(returnDayOffset)
                                     .ToString("yyyy-MM-dd");

            try
            {
                var returnSearchReq = new SearchSliceRequest(route.Destination, route.Origin, returnDate, paxCount, "Revenue");
                var returnSearchRes = await _retailApiClient.SearchSliceAsync(returnSearchReq, ct);

                var returnLegs = returnSearchRes.Itineraries.SelectMany(it => it.Legs).ToList();
                if (returnLegs.Count > 0)
                {
                    var returnLeg   = returnLegs[Random.Shared.Next(returnLegs.Count)];
                    var returnCabin = SelectCabin(returnLeg.Cabins);
                    returnOfferId   = returnCabin.FareFamilies[Random.Shared.Next(returnCabin.FareFamilies.Count)].Offer.OfferId;
                    returnSessionId = returnLeg.SessionId;
                }
                else
                {
                    hasReturn = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Simulator: return search failed for {Dest}→{Orig} — booking one-way", route.Destination, route.Origin);
                hasReturn = false;
            }
        }

        // ── Step 3: Create basket ──────────────────────────────────────────────
        var segments = new List<BasketSegment>
        {
            new(outboundOffer.OfferId, outboundLeg.SessionId)
        };

        if (hasReturn && returnOfferId is not null && returnSessionId is not null)
            segments.Add(new(returnOfferId, returnSessionId));

        var basketReq = new CreateBasketRequest(segments, "WEB", "GBP", "Revenue");
        var basketRes = await _retailApiClient.CreateBasketAsync(basketReq, ct);
        var basketId  = basketRes.BasketId;

        // ── Step 4: Get basket summary (reprice + validate offers) ─────────────
        await _retailApiClient.GetBasketSummaryAsync(basketId, ct);

        // ── Step 5: Add passengers ─────────────────────────────────────────────
        var passengers = GeneratePassengers(paxCount);
        await _retailApiClient.AddPassengersAsync(basketId, passengers, ct);

        // ── Step 6: Get basket to read inventoryId / basketItemId per segment ──
        var basket       = await _retailApiClient.GetBasketAsync(basketId, ct);
        var flightOffers = basket.BasketData.FlightOffers;

        if (flightOffers.Count == 0)
            throw new InvalidOperationException($"Basket {basketId} contains no flight offers after creation.");

        var outboundFlight = flightOffers[0];
        var returnFlight   = flightOffers.Count > 1 ? flightOffers[1] : null;

        // ── Step 7: Seatmaps + seat selection ──────────────────────────────────
        var allSeats = new List<SeatAssignment>();

        var outboundSeats = await TrySelectSeatsAsync(outboundFlight, paxCount, ct);
        allSeats.AddRange(outboundSeats);

        if (returnFlight is not null)
        {
            var returnSeats = await TrySelectSeatsAsync(returnFlight, paxCount, ct);
            allSeats.AddRange(returnSeats);
        }

        if (allSeats.Count > 0)
            await _retailApiClient.AddSeatsAsync(basketId, allSeats, ct);

        // ── Step 8: SSRs (applied to a subset of bookings) ────────────────────
        if (Random.Shared.Next(100) < SsrProbabilityPct)
        {
            try
            {
                var segmentInventoryIds = flightOffers.Select(f => f.InventoryId).ToList();
                var ssrs = GenerateSsrs(passengers, segmentInventoryIds);
                if (ssrs.Count > 0)
                    await _retailApiClient.AddSsrsAsync(basketId, ssrs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Simulator: SSR add failed for basket {BasketId} — continuing without SSRs", basketId);
            }
        }

        // ── Step 9: Confirm and pay ────────────────────────────────────────────
        var primary        = passengers[0];
        var confirmRequest = new ConfirmBasketRequest(
            new PaymentRequest(
                Method:         "CreditCard",
                CardNumber:     "4111111111111111",
                ExpiryDate:     "12/28",
                Cvv:            "737",
                CardholderName: $"{primary.GivenName} {primary.Surname}"),
            LoyaltyPointsToRedeem: null);

        var confirmResponse = await _retailApiClient.ConfirmBasketAsync(basketId, confirmRequest, ct);

        var routeLabel = $"{route.Origin}→{route.Destination}" + (hasReturn ? $" (return)" : " (one-way)");
        return (confirmResponse.OrderId, confirmResponse.BookingReference, routeLabel, hasReturn);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a randomly chosen leg whose departure is at least 1 hour from now
    /// and no more than 48 hours from now. Returns null when no legs qualify.
    /// </summary>
    private static SearchLeg? SelectValidLeg(SearchSliceResponse response, DateTime utcNow)
    {
        var earliest = utcNow.AddHours(1);
        var latest   = utcNow.AddHours(48);

        var validLegs = response.Itineraries
            .SelectMany(it => it.Legs)
            .Where(leg =>
            {
                var departure = ParseDeparture(leg.DepartureDate, leg.DepartureTime);
                return departure.HasValue && departure.Value > earliest && departure.Value <= latest;
            })
            .ToList();

        if (validLegs.Count == 0) return null;
        return validLegs[Random.Shared.Next(validLegs.Count)];
    }

    private static DateTime? ParseDeparture(string date, string time)
    {
        if (DateTime.TryParseExact(
                $"{date} {time}",
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// Selects a cabin using weighted random choice — Economy 60%, Premium Economy 25%,
    /// Business 15%. Falls back to a uniform random pick if none of the preferred cabins
    /// are available on this flight.
    /// </summary>
    private static SearchCabin SelectCabin(List<SearchCabin> cabins)
    {
        var available = cabins.ToDictionary(c => c.CabinCode, StringComparer.OrdinalIgnoreCase);

        var eligible = CabinWeights.Where(cw => available.ContainsKey(cw.Code)).ToList();
        if (eligible.Count == 0)
            return cabins[Random.Shared.Next(cabins.Count)];

        var totalWeight = eligible.Sum(cw => cw.Weight);
        var roll        = Random.Shared.Next(totalWeight);
        var cumulative  = 0;

        foreach (var (code, weight) in eligible)
        {
            cumulative += weight;
            if (roll < cumulative)
                return available[code];
        }

        return cabins[0]; // unreachable, but satisfies the compiler
    }

    /// <summary>
    /// Fetches the seatmap for a flight and returns seat assignments for each passenger.
    /// Returns an empty list (rather than throwing) when the seatmap is unavailable
    /// or there are insufficient available seats, so the booking can continue without seats.
    /// </summary>
    private async Task<List<SeatAssignment>> TrySelectSeatsAsync(
        BasketFlightOffer flight, int paxCount, CancellationToken ct)
    {
        try
        {
            var seatmap = await _retailApiClient.GetSeatmapAsync(
                flight.InventoryId, flight.AircraftType, flight.FlightNumber, flight.CabinCode, ct);

            var available = seatmap.Cabins
                .SelectMany(c => c.Seats)
                .Where(s => string.Equals(s.Availability, "available", StringComparison.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next())
                .Take(paxCount)
                .ToList();

            if (available.Count < paxCount)
                return [];

            return available.Select((seat, idx) => new SeatAssignment(
                PassengerId:   $"PAX-{idx + 1}",
                SegmentId:     flight.InventoryId,
                BasketItemRef: flight.BasketItemId,
                SeatOfferId:   seat.SeatOfferId,
                SeatNumber:    seat.SeatNumber,
                SeatPosition:  seat.Position,
                CabinCode:     seat.CabinCode,
                Price:         seat.Price,
                Currency:      seat.Currency)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Simulator: seatmap unavailable for flight {FlightNumber} — continuing without seats",
                flight.FlightNumber);
            return [];
        }
    }

    /// <summary>
    /// Assigns a random SSR code to a randomly chosen subset of passengers (at least one),
    /// applied across every segment in the booking.
    /// </summary>
    private static List<SsrRequest> GenerateSsrs(List<PassengerRequest> passengers, List<string> segmentInventoryIds)
    {
        var ssrs = new List<SsrRequest>();

        // Pick 1 to all passengers to receive an SSR
        var passengerCount = Random.Shared.Next(1, passengers.Count + 1);
        var selectedPax    = passengers.OrderBy(_ => Random.Shared.Next()).Take(passengerCount).ToList();

        foreach (var pax in selectedPax)
        {
            var ssrCode = SsrCodes[Random.Shared.Next(SsrCodes.Length)];
            foreach (var segmentId in segmentInventoryIds)
                ssrs.Add(new SsrRequest(ssrCode, pax.PassengerId, segmentId));
        }

        return ssrs;
    }

    private static List<PassengerRequest> GeneratePassengers(int count)
    {
        var passengers = new List<PassengerRequest>(count);

        for (var i = 0; i < count; i++)
        {
            var given   = FirstNames[Random.Shared.Next(FirstNames.Length)];
            var surname = LastNames[Random.Shared.Next(LastNames.Length)];
            var dob     = DateTime.UtcNow
                              .AddYears(-Random.Shared.Next(20, 70))
                              .AddDays(-Random.Shared.Next(0, 365))
                              .ToString("yyyy-MM-dd");
            var gender  = Random.Shared.Next(2) == 0 ? "M" : "F";
            var email   = $"{given.ToLowerInvariant()}.{surname.ToLowerInvariant()}{Random.Shared.Next(100, 999)}@simulator.apexair.com";
            var phone   = $"+447{Random.Shared.Next(100_000_000, 999_999_999)}";

            passengers.Add(new PassengerRequest(
                PassengerId:   $"PAX-{i + 1}",
                Type:          "ADT",
                GivenName:     given,
                Surname:       surname,
                Dob:           dob,
                Gender:        gender,
                LoyaltyNumber: null,
                Contacts:      new PassengerContacts(email, phone),
                Docs:          []));
        }

        return passengers;
    }
}
