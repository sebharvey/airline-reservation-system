using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Application.RunSimulator;
using ReservationSystem.Simulator.Application.UpdateFlightOperationalData;

namespace ReservationSystem.Simulator.Functions;

/// <summary>
/// Timer trigger that fires every 40 minutes and creates 1–6 confirmed orders
/// across random routes over the next 48 hours. Simulates realistic web booking
/// activity with a mix of one-way and return journeys.
///
/// Also exposes a manual HTTP trigger at GET /api/v1/simulator/run so the same
/// process can be started on demand from a browser or curl.
/// </summary>
public sealed class SimulatorFunction
{
    private readonly RunSimulatorHandler                    _handler;
    private readonly UpdateFlightOperationalDataHandler     _flightUpdateHandler;
    private readonly ILogger<SimulatorFunction>             _logger;

    public SimulatorFunction(
        RunSimulatorHandler                handler,
        UpdateFlightOperationalDataHandler flightUpdateHandler,
        ILogger<SimulatorFunction>         logger)
    {
        _handler             = handler;
        _flightUpdateHandler = flightUpdateHandler;
        _logger              = logger;
    }

    // Runs every 40 minutes: "0 */40 * * * *"
    [Function("Simulator")]
    public async Task Run(
        [TimerTrigger("0 */40 * * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("Simulator timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }

    // Manual trigger: GET /api/v1/simulator/run
    [Function("SimulatorManualTrigger")]
    public async Task<HttpResponseData> RunManual(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/simulator/run")] HttpRequestData req,
        CancellationToken ct)
    {
        _logger.LogInformation("Simulator manual trigger invoked at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync("{\"message\":\"Simulator run completed.\"}");
        return response;
    }

    // Runs every 20 minutes: "0 */20 * * * *"
    [Function("FlightOperationalDataSimulator")]
    public async Task RunFlightUpdates(
        [TimerTrigger("0 */20 * * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("FlightUpdate timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _flightUpdateHandler.HandleAsync(ct);
    }

    // Manual trigger: GET /api/v1/simulator/flight-updates
    [Function("FlightOperationalDataSimulatorManualTrigger")]
    public async Task<HttpResponseData> RunFlightUpdatesManual(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/simulator/flight-updates")] HttpRequestData req,
        CancellationToken ct)
    {
        _logger.LogInformation("FlightUpdate manual trigger invoked at {UtcNow:O}", DateTime.UtcNow);

        await _flightUpdateHandler.HandleAsync(ct);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync("{\"message\":\"Flight operational data update completed.\"}");
        return response;
    }
}
