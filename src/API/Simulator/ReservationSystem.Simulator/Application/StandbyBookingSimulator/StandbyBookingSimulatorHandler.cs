using System.Globalization;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.StandbyBookingSimulator;

/// <summary>
/// Creates 1–3 standby bookings per run using the Staff fare family (zero base fare, tax only).
/// Staff travel is always a single passenger booked in Economy (Y) on a direct route.
/// Standby orders are placed via the admin search and basket endpoints, which surface the
/// private Staff fare that is hidden from the public retail channel.
/// Intended to be invoked alongside the main simulator every 40 minutes.
/// </summary>
public sealed class StandbyBookingSimulatorHandler
{
    private const int MinStandbyOrders = 1;
    private const int MaxStandbyOrders = 3;

    private static readonly (string Origin, string Destination, int Weight)[] Routes =
    [
        ("LHR", "JFK", 3),
        ("LHR", "MIA", 1),
        ("LHR", "DEL", 1),
        ("JFK", "LHR", 3),
        ("MIA", "LHR", 1),
    ];

    private static readonly string[] FirstNames =
    [
        "James", "Oliver", "Harry", "Amelia", "Olivia", "Isla", "Emily",
        "Aryan", "Priya", "Wei", "Mei", "Kwame", "Nadia", "Zara", "Elena",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Jones", "Williams", "Taylor", "Brown", "Davies", "Evans",
        "Khan", "Patel", "Singh", "Zhang", "Nakamura", "Okafor", "Rossi",
    ];

    private readonly IAdminApiClient  _adminApiClient;
    private readonly IRetailApiClient _retailApiClient;
    private readonly ILogger<StandbyBookingSimulatorHandler> _logger;

    public StandbyBookingSimulatorHandler(
        IAdminApiClient  adminApiClient,
        IRetailApiClient retailApiClient,
        ILogger<StandbyBookingSimulatorHandler> logger)
    {
        _adminApiClient  = adminApiClient;
        _retailApiClient = retailApiClient;
        _logger          = logger;
    }

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var orderCount = Random.Shared.Next(MinStandbyOrders, MaxStandbyOrders + 1);
        var created    = 0;

        _logger.LogInformation("StandbySimulator: starting run — targeting {Count} standby orders", orderCount);

        string? bearerToken = null;

        for (var i = 0; i < orderCount; i++)
        {
            try
            {
                // Obtain (or reuse) a staff JWT for the admin endpoints.
                bearerToken ??= await _adminApiClient.LoginAsync(ct);

                var result = await CreateStandbyOrderAsync(bearerToken, ct);

                if (result is null)
                {
                    _logger.LogDebug(
                        "StandbySimulator: order {Index}/{Total} skipped — no suitable standby flight found",
                        i + 1, orderCount);
                    continue;
                }

                var (orderId, bookingRef, route) = result.Value;
                created++;
                _logger.LogInformation(
                    "StandbySimulator: order {Index}/{Total} created — orderId={OrderId} ref={BookingRef} route={Route}",
                    i + 1, orderCount, orderId, bookingRef, route);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "StandbySimulator: order {Index}/{Total} failed — skipping to next",
                    i + 1, orderCount);

                // Reset the token on auth failures so the next iteration re-authenticates.
                bearerToken = null;
            }
        }

        _logger.LogInformation(
            "StandbySimulator: run complete — {Created}/{Total} standby orders created",
            created, orderCount);
    }

    private async Task<(string OrderId, string BookingRef, string Route)?> CreateStandbyOrderAsync(
        string bearerToken, CancellationToken ct)
    {
        var now   = DateTime.UtcNow;
        var route = Routes[WeightedRandomIndex(Routes.Select(r => r.Weight).ToArray())];

        var departureDateOffset = Random.Shared.Next(0, 2);
        var departureDate       = now.Date.AddDays(departureDateOffset).ToString("yyyy-MM-dd");

        // ── Step 1: Admin search with bookingType=Standby ─────────────────────
        var searchReq = new SearchSliceRequest(route.Origin, route.Destination, departureDate, 1, "Standby");
        var searchRes = await _retailApiClient.AdminSearchSliceAsync(searchReq, bearerToken, ct);

        // ── Step 2: Pick a valid leg and locate the Staff fare ─────────────────
        var earliest = now.AddHours(1);
        var latest   = now.AddHours(48);

        var candidates = searchRes.Itineraries
            .SelectMany(it => it.Segments)
            .SelectMany(seg => seg.Flights)
            .Select(leg => (Leg: leg, Departure: ParseDeparture(leg.DepartureDate, leg.DepartureTime)))
            .Where(x => x.Departure.HasValue && x.Departure.Value > earliest && x.Departure.Value <= latest)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var (selectedLeg, _) = candidates[Random.Shared.Next(candidates.Count)];

        var economyCabin = selectedLeg.Cabins.FirstOrDefault(c =>
            string.Equals(c.CabinCode, "Y", StringComparison.OrdinalIgnoreCase));

        if (economyCabin is null)
            return null;

        var staffFamily = economyCabin.FareFamilies.FirstOrDefault(ff =>
            string.Equals(ff.FareFamily, "Staff", StringComparison.OrdinalIgnoreCase));

        if (staffFamily is null)
            return null;

        // ── Step 3: Create admin basket with bookingType=Standby ──────────────
        var segments  = new List<BasketSegment> { new(staffFamily.Offer.OfferId, selectedLeg.SessionId) };
        var basketReq = new CreateBasketRequest(segments, "GBP", "Standby");
        var basketRes = await _retailApiClient.AdminCreateBasketAsync(basketReq, bearerToken, ct);
        var basketId  = basketRes.BasketId;

        // ── Step 4: Get basket summary ─────────────────────────────────────────
        await _retailApiClient.GetBasketSummaryAsync(basketId, ct);

        // ── Step 5: Add single passenger ──────────────────────────────────────
        var passenger  = GenerateStaffPassenger();
        await _retailApiClient.AddPassengersAsync(basketId, [passenger], ct);

        // ── Step 6: Confirm with payment (taxes only — base fare is zero) ──────
        var confirmReq = new ConfirmBasketRequest(
            ChannelCode: "STAFF",
            new PaymentRequest(
                Method:         "CreditCard",
                CardNumber:     "4111111111111111",
                ExpiryDate:     "12/28",
                Cvv:            "737",
                CardholderName: $"{passenger.GivenName} {passenger.Surname}"),
            LoyaltyPointsToRedeem: null);

        var confirmRes = await _retailApiClient.ConfirmBasketAsync(basketId, confirmReq, ct);

        var routeLabel = $"{route.Origin}→{route.Destination}";
        return (confirmRes.OrderId, confirmRes.BookingReference, routeLabel);
    }

    private static PassengerRequest GenerateStaffPassenger()
    {
        var given   = FirstNames[Random.Shared.Next(FirstNames.Length)];
        var surname = LastNames[Random.Shared.Next(LastNames.Length)];
        var dob     = DateTime.UtcNow
                          .AddYears(-Random.Shared.Next(22, 60))
                          .AddDays(-Random.Shared.Next(0, 365))
                          .ToString("yyyy-MM-dd");
        var gender  = Random.Shared.Next(2) == 0 ? "M" : "F";
        var email   = $"{given.ToLowerInvariant()}.{surname.ToLowerInvariant()}{Random.Shared.Next(100, 999)}@staff.apexair.com";
        var phone   = $"+447{Random.Shared.Next(100_000_000, 999_999_999)}";

        return new PassengerRequest(
            PassengerId:   "PAX-1",
            Type:          "ADT",
            GivenName:     given,
            Surname:       surname,
            Dob:           dob,
            Gender:        gender,
            LoyaltyNumber: null,
            Contacts:      new PassengerContacts(email, phone),
            Docs:          []);
    }

    private static int WeightedRandomIndex(int[] weights)
    {
        var total      = weights.Sum();
        var roll       = Random.Shared.Next(total);
        var cumulative = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return i;
        }

        return 0;
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
}
