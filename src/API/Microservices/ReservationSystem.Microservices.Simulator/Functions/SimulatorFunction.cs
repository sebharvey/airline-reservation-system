using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Simulator.Application.RunSimulator;

namespace ReservationSystem.Microservices.Simulator.Functions;

/// <summary>
/// Hourly timer trigger that creates 5 confirmed orders for the next day's
/// AX001 (LHR → JFK) flight, each with a random passenger count (1–6).
/// Simulates realistic booking activity for testing and demonstration.
/// </summary>
public sealed class SimulatorFunction
{
    private readonly RunSimulatorHandler _handler;
    private readonly ILogger<SimulatorFunction> _logger;

    public SimulatorFunction(
        RunSimulatorHandler handler,
        ILogger<SimulatorFunction> logger)
    {
        _handler = handler;
        _logger  = logger;
    }

    // Runs every 60 minutes at the top of the hour: "0 0 * * * *"
    [Function("Simulator")]
    public async Task Run(
        [TimerTrigger("0 0 * * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("Simulator timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
