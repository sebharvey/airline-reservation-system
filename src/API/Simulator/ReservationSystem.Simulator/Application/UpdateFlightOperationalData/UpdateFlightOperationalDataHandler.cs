using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.UpdateFlightOperationalData;

/// <summary>
/// Simulates pre-departure operational updates to flight inventory:
/// <list type="bullet">
///   <item>~24 hours before departure — assigns an aircraft registration (e.g. G-X123).</item>
///   <item>~1 hour before departure  — assigns a random departure gate (1–50).</item>
/// </list>
/// Runs on a timer trigger every 20 minutes. Each run authenticates with the Admin API,
/// then queries today's and tomorrow's inventory via the Retail API and updates any flights
/// that fall within the relevant time window and have not yet been assigned the value.
/// </summary>
public sealed class UpdateFlightOperationalDataHandler
{
    // Window (hours before departure) within which aircraft registration is assigned.
    private const int RegistrationWindowOpenHours  = 26;
    private const int RegistrationWindowCloseHours = 20;

    // Window (minutes before departure) within which the departure gate is assigned.
    private const int GateWindowOpenMinutes  = 90;
    private const int GateWindowCloseMinutes = 40;

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
