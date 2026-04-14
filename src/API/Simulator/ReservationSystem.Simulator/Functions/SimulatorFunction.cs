using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Application.RunSimulator;

namespace ReservationSystem.Simulator.Functions;

/// <summary>
/// Timer trigger that fires every 20 minutes and creates 1–6 confirmed orders
/// across random routes over the next 48 hours. Simulates realistic web booking
/// activity with a mix of one-way and return journeys.
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

    // Runs every 20 minutes: "0 */20 * * * *"
    [Function("Simulator")]
    public async Task Run(
        [TimerTrigger("0 */20 * * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("Simulator timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
