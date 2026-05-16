using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Domain.ExternalServices;
using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Application.DailyAircraftGateAssignment;

/// <summary>
/// Runs once a day at 01:00 UTC and bulk-assigns aircraft registrations and departure gates
/// to every flight in today's inventory that does not yet have them.
///
/// Unlike the rolling UpdateFlightOperationalDataHandler, which assigns values progressively
/// as flights enter time windows throughout the day, this handler assigns both values up-front
/// for the entire day in a single pass — ensuring no flight departs without an aircraft
/// registration or gate even if an interim run was missed.
/// </summary>
public sealed class DailyAircraftGateAssignmentHandler
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly IFlightUpdateClient _flightUpdateClient;
    private readonly IConfiguration      _configuration;
    private readonly ILogger<DailyAircraftGateAssignmentHandler> _logger;

    public DailyAircraftGateAssignmentHandler(
        IFlightUpdateClient flightUpdateClient,
        IConfiguration      configuration,
        ILogger<DailyAircraftGateAssignmentHandler> logger)
    {
        _flightUpdateClient = flightUpdateClient;
        _configuration      = configuration;
        _logger             = logger;
    }

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var username = _configuration["User:Username"];
        var userPwd  = _configuration["User:Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(userPwd))
        {
            _logger.LogWarning("DailyAssignment: User:Username or User:Password not configured — skipping run.");
            return;
        }

        string jwtToken;
        try
        {
            jwtToken = await _flightUpdateClient.LoginAsync(username, userPwd, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyAssignment: login failed — aborting run.");
            return;
        }

        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        List<FlightInventoryItem> flights;
        try
        {
            flights = await _flightUpdateClient.GetInventoryAsync(today, jwtToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DailyAssignment: failed to fetch inventory for {Date} — aborting run.", today);
            return;
        }

        var registrationUpdated = 0;
        var gateUpdated         = 0;

        foreach (var flight in flights)
        {
            if (flight.AircraftRegistration is null)
            {
                var registration = GenerateRegistration();
                try
                {
                    await _flightUpdateClient.SetOperationalDataAsync(
                        flight.InventoryId, null, registration, jwtToken, ct);
                    registrationUpdated++;
                    _logger.LogInformation(
                        "DailyAssignment: assigned registration {Registration} to {FlightNumber} on {Date}",
                        registration, flight.FlightNumber, flight.DepartureDate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "DailyAssignment: failed to set registration for {FlightNumber} on {Date}",
                        flight.FlightNumber, flight.DepartureDate);
                }
            }

            if (flight.DepartureGate is null)
            {
                var gate = Random.Shared.Next(1, 51).ToString();
                try
                {
                    await _flightUpdateClient.SetOperationalDataAsync(
                        flight.InventoryId, gate, null, jwtToken, ct);
                    gateUpdated++;
                    _logger.LogInformation(
                        "DailyAssignment: assigned gate {Gate} to {FlightNumber} on {Date}",
                        gate, flight.FlightNumber, flight.DepartureDate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "DailyAssignment: failed to set gate for {FlightNumber} on {Date}",
                        flight.FlightNumber, flight.DepartureDate);
                }
            }
        }

        _logger.LogInformation(
            "DailyAssignment: run complete for {Date} — {RegistrationUpdated} registration(s) assigned, {GateUpdated} gate(s) assigned",
            today, registrationUpdated, gateUpdated);
    }

    private static string GenerateRegistration()
    {
        var letter = Letters[Random.Shared.Next(Letters.Length)];
        var digits = Random.Shared.Next(100, 1000);
        return $"G-{letter}{digits}";
    }
}
