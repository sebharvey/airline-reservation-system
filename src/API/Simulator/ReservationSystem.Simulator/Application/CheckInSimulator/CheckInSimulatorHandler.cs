using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.CheckInSimulator;

/// <summary>
/// Simulates agent check-in for flights departing in the next 24 hours.
///
/// Replicates the terminal app check-in flow (POST /v1/admin/checkin/{bookingRef}):
/// <list type="bullet">
///   <item>Authenticates with the Admin API using staff credentials.</item>
///   <item>Fetches all flights departing in the next 24 hours via the Retail API admin inventory.</item>
///   <item>For each eligible flight (departure between 1 h and 24 h from now), retrieves the
///         passenger manifest and calculates a time-proportional check-in target so that 95 %
///         of passengers are checked in by 1 hour before departure.</item>
///   <item>Groups unchecked-in passengers by booking reference and calls the Operations API
///         agent check-in endpoint once per booking with randomly generated travel documents.</item>
///   <item>Uses overrideTimatic = true with a simulator reason so that Timatic / watchlist
///         checks never block the automated run.</item>
/// </list>
/// Intended to run every 15 minutes on a timer trigger.
/// </summary>
public sealed class CheckInSimulatorHandler
{
    // ── Configuration ───────────────────────────────────────────────────────────

    /// <summary>95 % of passengers should be checked in 1 hour before departure.</summary>
    private const double CheckInTargetFraction = 0.95;

    /// <summary>Check-in window opens this many hours before departure (matches OLCI window).</summary>
    private const double CheckInWindowOpenHours = 24.0;

    /// <summary>Check-in window closes this many hours before departure.</summary>
    private const double CheckInWindowCloseHours = 1.0;

    private const string OverrideReason = "Simulator automated check-in";

    // ── Country pool — realistic passport-issuing countries ─────────────────────

    private static readonly string[] Countries =
    [
        "GBR", "USA", "FRA", "DEU", "ESP", "ITA", "AUS", "CAN", "NLD", "SWE",
        "NOR", "DNK", "IRL", "JPN", "SGP", "ZAF", "NZL", "CHE", "AUT", "BEL",
        "IND", "PAK", "BGD", "LKA", "KEN", "NGA", "GHA", "BRA", "MEX", "ARG",
    ];

    private static readonly char[] AlphaChars =
        "ABCDEFGHJKLMNPRSTUVWXYZ".ToCharArray();

    private static readonly char[] DigitChars =
        "0123456789".ToCharArray();

    // ── Dependencies ────────────────────────────────────────────────────────────

    private readonly IFlightUpdateClient _flightUpdateClient;
    private readonly IConfiguration      _configuration;
    private readonly ILogger<CheckInSimulatorHandler> _logger;

    public CheckInSimulatorHandler(
        IFlightUpdateClient flightUpdateClient,
        IConfiguration      configuration,
        ILogger<CheckInSimulatorHandler> logger)
    {
        _flightUpdateClient = flightUpdateClient;
        _configuration      = configuration;
        _logger             = logger;
    }

    // ── Entry point ─────────────────────────────────────────────────────────────

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var username = _configuration["User:Username"];
        var password = _configuration["User:Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("CheckIn: User:Username or User:Password not configured — skipping run.");
            return;
        }

        string jwtToken;
        try
        {
            jwtToken = await _flightUpdateClient.LoginAsync(username, password, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckIn: admin login failed — aborting run.");
            return;
        }

        var now      = DateTime.UtcNow;
        var today    = now.Date.ToString("yyyy-MM-dd");
        var tomorrow = now.Date.AddDays(1).ToString("yyyy-MM-dd");

        var flights = await FetchFlightsAsync(jwtToken, [today, tomorrow], ct);

        var totalCheckedIn = 0;
        var totalSkipped   = 0;

        foreach (var flight in flights)
        {
            if (!TryParseDeparture(flight.DepartureDate, flight.DepartureTime, out var departure))
                continue;

            var hoursUntil = (departure - now).TotalHours;

            if (hoursUntil < CheckInWindowCloseHours || hoursUntil > CheckInWindowOpenHours)
                continue;

            // progress = 0 at window open (24 h out), 1 at window close (1 h out)
            var windowDuration = CheckInWindowOpenHours - CheckInWindowCloseHours; // 23 h
            var progress       = (CheckInWindowOpenHours - hoursUntil) / windowDuration;
            var targetFraction = progress * CheckInTargetFraction;

            var checkedIn = await ProcessFlightAsync(
                flight, targetFraction, jwtToken, ct);

            totalCheckedIn += checkedIn;

            if (checkedIn == 0)
                totalSkipped++;
        }

        _logger.LogInformation(
            "CheckIn: run complete — {CheckedIn} passenger(s) checked in across {Flights} eligible flight(s), {Skipped} already at target",
            totalCheckedIn, flights.Count, totalSkipped);
    }

    // ── Per-flight processing ───────────────────────────────────────────────────

    private async Task<int> ProcessFlightAsync(
        FlightInventoryItem flight,
        double targetFraction,
        string jwtToken,
        CancellationToken ct)
    {
        FlightManifestResponse manifest;
        try
        {
            manifest = await _flightUpdateClient.GetManifestAsync(
                flight.FlightNumber, flight.DepartureDate, jwtToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CheckIn: failed to fetch manifest for {FlightNumber} on {Date} — skipping",
                flight.FlightNumber, flight.DepartureDate);
            return 0;
        }

        var total          = manifest.Entries.Count;
        var alreadyChecked = manifest.Entries.Count(e => e.CheckedIn);

        if (total == 0)
            return 0;

        var targetCount = (int)Math.Ceiling(total * targetFraction);
        var toCheckIn   = targetCount - alreadyChecked;

        if (toCheckIn <= 0)
            return 0;

        _logger.LogInformation(
            "CheckIn: {FlightNumber} {Date} — {Already}/{Total} checked in, target {Target} ({Pct:P0}), checking in {ToCheckIn} more",
            flight.FlightNumber, flight.DepartureDate,
            alreadyChecked, total, targetCount,
            targetFraction, toCheckIn);

        // Pick random unchecked-in passengers up to the required count
        var candidates = manifest.Entries
            .Where(e => !e.CheckedIn)
            .OrderBy(_ => Random.Shared.Next())
            .Take(toCheckIn)
            .GroupBy(e => e.BookingReference)
            .ToList();

        var checkedIn = 0;

        foreach (var bookingGroup in candidates)
        {
            var bookingRef = bookingGroup.Key;

            var submissions = bookingGroup.Select(e => new CheckInPaxSubmission(
                e.ETicketNumber,
                GenerateTravelDocument())).ToList();

            var request = new AdminCheckInRequest(
                DepartureAirport: flight.Origin,
                Passengers:       submissions,
                OverrideTimatic:  true,
                OverrideReason:   OverrideReason);

            try
            {
                await _flightUpdateClient.AdminCheckInAsync(bookingRef, request, jwtToken, ct);
                checkedIn += submissions.Count;

                _logger.LogDebug(
                    "CheckIn: checked in {Count} pax on booking {BookingRef} for {FlightNumber}",
                    submissions.Count, bookingRef, flight.FlightNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CheckIn: failed to check in booking {BookingRef} for {FlightNumber} — skipping",
                    bookingRef, flight.FlightNumber);
            }
        }

        return checkedIn;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<FlightInventoryItem>> FetchFlightsAsync(
        string jwtToken, string[] dates, CancellationToken ct)
    {
        var all = new List<FlightInventoryItem>();
        foreach (var date in dates)
        {
            try
            {
                var flights = await _flightUpdateClient.GetInventoryAsync(date, jwtToken, ct);
                all.AddRange(flights);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CheckIn: failed to fetch inventory for {Date}", date);
            }
        }
        return all;
    }

    private static bool TryParseDeparture(string date, string time, out DateTime departure)
    {
        return DateTime.TryParseExact(
            $"{date} {time}",
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out departure);
    }

    /// <summary>
    /// Generates a realistic random travel document matching the same approach
    /// used by the terminal app's passport scanner simulation.
    /// </summary>
    private static SimTravelDocument GenerateTravelDocument()
    {
        var country = Countries[Random.Shared.Next(Countries.Length)];

        // Two alpha chars + seven digits (e.g. AB1234567)
        var docNumber =
            AlphaChars[Random.Shared.Next(AlphaChars.Length)].ToString() +
            AlphaChars[Random.Shared.Next(AlphaChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString() +
            DigitChars[Random.Shared.Next(DigitChars.Length)].ToString();

        var issueYear  = 2019 + Random.Shared.Next(6); // 2019–2024
        var expiryYear = 2029 + Random.Shared.Next(8); // 2029–2036
        var issueMonth = Random.Shared.Next(1, 13);
        var issueDay   = Random.Shared.Next(1, 29);
        var expMonth   = Random.Shared.Next(1, 13);
        var expDay     = Random.Shared.Next(1, 29);

        var issueDate  = $"{issueYear}-{issueMonth:D2}-{issueDay:D2}";
        var expiryDate = $"{expiryYear}-{expMonth:D2}-{expDay:D2}";

        return new SimTravelDocument(
            Type:          "PASSPORT",
            Number:        docNumber,
            IssuingCountry: country,
            Nationality:   country,
            IssueDate:     issueDate,
            ExpiryDate:    expiryDate);
    }
}
