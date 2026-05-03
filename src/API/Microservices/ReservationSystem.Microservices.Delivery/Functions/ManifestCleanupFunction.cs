using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.DeleteExpiredManifestItems;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class ManifestCleanupFunction
{
    private readonly DeleteExpiredManifestItemsHandler _handler;
    private readonly ILogger<ManifestCleanupFunction> _logger;

    public ManifestCleanupFunction(
        DeleteExpiredManifestItemsHandler handler,
        ILogger<ManifestCleanupFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // Runs daily at 01:00 UTC: "0 0 1 * * *"
    [Function("DeleteExpiredManifestItems")]
    public async Task Run(
        [TimerTrigger("0 0 1 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredManifestItems timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
