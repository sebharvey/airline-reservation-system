using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.RollingInventoryImport;

namespace ReservationSystem.Microservices.Offer.Functions;

/// <summary>
/// Daily timer trigger that extends the rolling inventory window by importing the next
/// day of flights at the 3-month boundary. Runs at 01:00 UTC, after the midnight cleanup
/// has removed expired inventory.
/// </summary>
public sealed class RollingInventoryImportFunction
{
    private readonly RollingInventoryImportHandler _handler;
    private readonly ILogger<RollingInventoryImportFunction> _logger;

    public RollingInventoryImportFunction(
        RollingInventoryImportHandler handler,
        ILogger<RollingInventoryImportFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // Runs once a day at 01:00 UTC: "0 0 1 * * *"
    [Function("RollingInventoryImport")]
    public async Task Run(
        [TimerTrigger("0 0 1 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("RollingInventoryImport timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
