using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.DeleteExpiredFlightInventory;

namespace ReservationSystem.Microservices.Offer.Functions;

public sealed class OfferCleanupFunction
{
    private readonly DeleteExpiredFlightInventoryHandler _handler;
    private readonly ILogger<OfferCleanupFunction> _logger;

    public OfferCleanupFunction(
        DeleteExpiredFlightInventoryHandler handler,
        ILogger<OfferCleanupFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // Runs once a day at midnight UTC: "0 0 0 * * *"
    [Function("DeleteExpiredFlightInventory")]
    public async Task Run(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredFlightInventory timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
