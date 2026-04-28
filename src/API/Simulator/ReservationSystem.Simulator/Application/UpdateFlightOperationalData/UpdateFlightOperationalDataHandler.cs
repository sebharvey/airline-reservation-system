using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.UpdateFlightOperationalData;

/// <summary>
/// Simulates pre-departure operational updates to flight inventory:
/// <list type="bullet">
///   <item>1–26 hours before departure — assigns an aircraft registration (e.g. G-X123) if not already set.
///         Primary assignment happens ~24 hours out; the wide lower bound catches any flight
///         that was missed on a previous run.</item>
///   <item>60–180 minutes before departure — assigns a random departure gate (1–50) if not already set.
///         The 60-minute lower bound guarantees the gate is always set before the last hour.</item>
/// </list>
/// Runs on a timer trigger every 20 minutes. Each run authenticates with the Admin API,
/// then queries today's and tomorrow's inventory via the Retail API and updates any flights
/// that fall within the relevant time window and have not yet been assigned the value.
/// </summary>
public sealed class UpdateFlightOperationalDataHandler
{
    // Any flight departing within this many hours that has no registration gets one assigned.
    // Wide lower bound acts as a catch-up so no flight departs without a registration.
    private const int RegistrationWindowOpenHours  = 26;
    private const int RegistrationWindowCloseHours = 1;

    // Gate is assigned between 60 and 180 minutes before departure.
    // The 60-minute floor guarantees the gate is always set before the last hour.
    private const int GateWindowOpenMinutes  = 180;
    private const int GateWindowCloseMinutes = 60;

    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly IFlightUpdateClient _flightUpdateClient;
    private readonly IConfiguration      _configuration;
    private readonly ILogger<UpdateFlightOperationalDataHandler> _logger;

    public UpdateFlightOperationalDataHandler(
        IFlightUpdateClient flightUpdateClient,
        IConfiguration      configuration,
        ILogger<UpdateFlightOperationalDataHandler> logger)
    {
        _flightUpdateClient = flightUpdateClient;
        _configuration      = configuration;
        _logger             = logger;
    }

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var username = _configuration["User:Username"];
        var password = _configuration["User:Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("FlightUpdate: User:Username or User:Password not configured — skipping run.");
            return;
        }

        string jwtToken;
        try
        {
            jwtToken = await _flightUpdateClient.LoginAsync(username, password, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlightUpdate: login failed — aborting run.");
            return;
        }

        var now      = DateTime.UtcNow;
        var today    = now.Date.ToString("yyyy-MM-dd");
        var tomorrow = now.Date.AddDays(1).ToString("yyyy-MM-dd");

        var flights = await FetchInventoryAsync(jwtToken, [today, tomorrow], ct);

        var registrationUpdated = 0;
        var gateUpdated         = 0;

        foreach (var flight in flights)
        {
            if (!TryParseDeparture(flight.DepartureDate, flight.DepartureTime, out var departure))
                continue;

            var hoursUntilDeparture   = (departure - now).TotalHours;
            var minutesUntilDeparture = (departure - now).TotalMinutes;

            // ── Aircraft registration (~24 h before departure) ─────────────────
            if (flight.AircraftRegistration is null
                && hoursUntilDeparture >= RegistrationWindowCloseHours
                && hoursUntilDeparture <= RegistrationWindowOpenHours)
            {
                var registration = GenerateRegistration();
                try
                {
                    await _flightUpdateClient.SetOperationalDataAsync(
                        flight.InventoryId, null, registration, jwtToken, ct);
                    registrationUpdated++;
                    _logger.LogInformation(
                        "FlightUpdate: assigned registration {Registration} to {FlightNumber} on {Date} ({Hours:F1}h before departure)",
                        registration, flight.FlightNumber, flight.DepartureDate, hoursUntilDeparture);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "FlightUpdate: failed to set registration for {FlightNumber} on {Date}",
                        flight.FlightNumber, flight.DepartureDate);
                }
            }

            // ── Departure gate (~1 h before departure) ─────────────────────────
            if (flight.DepartureGate is null
                && minutesUntilDeparture >= GateWindowCloseMinutes
                && minutesUntilDeparture <= GateWindowOpenMinutes)
            {
                var gate = Random.Shared.Next(1, 51).ToString();
                try
                {
                    await _flightUpdateClient.SetOperationalDataAsync(
                        flight.InventoryId, gate, null, jwtToken, ct);
                    gateUpdated++;
                    _logger.LogInformation(
                        "FlightUpdate: assigned gate {Gate} to {FlightNumber} on {Date} ({Minutes:F0}min before departure)",
                        gate, flight.FlightNumber, flight.DepartureDate, minutesUntilDeparture);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "FlightUpdate: failed to set gate for {FlightNumber} on {Date}",
                        flight.FlightNumber, flight.DepartureDate);
                }
            }
        }

        _logger.LogInformation(
            "FlightUpdate: run complete — {RegistrationUpdated} registration(s) assigned, {GateUpdated} gate(s) assigned",
            registrationUpdated, gateUpdated);
    }

    private async Task<List<FlightInventoryItem>> FetchInventoryAsync(
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
                _logger.LogWarning(ex, "FlightUpdate: failed to fetch inventory for {Date}", date);
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

    private static string GenerateRegistration()
    {
        var letter = Letters[Random.Shared.Next(Letters.Length)];
        var digits = Random.Shared.Next(100, 1000);
        return $"G-{letter}{digits}";
    }
}
