using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.DeleteExpiredBaskets;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class BasketCleanupFunction
{
    private readonly DeleteExpiredBasketsHandler _handler;
    private readonly ILogger<BasketCleanupFunction> _logger;

    public BasketCleanupFunction(
        DeleteExpiredBasketsHandler handler,
        ILogger<BasketCleanupFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // Runs daily at midnight UTC: "0 0 0 * * *"
    [Function("DeleteExpiredBaskets")]
    public async Task Run(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredBaskets timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
