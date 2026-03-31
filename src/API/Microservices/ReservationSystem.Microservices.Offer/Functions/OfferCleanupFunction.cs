using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.DeleteExpiredFlightInventory;
using ReservationSystem.Microservices.Offer.Application.DeleteExpiredStoredOffers;

namespace ReservationSystem.Microservices.Offer.Functions;

public sealed class OfferCleanupFunction
{
    private readonly DeleteExpiredFlightInventoryHandler _deleteFlightInventoryHandler;
    private readonly DeleteExpiredStoredOffersHandler _deleteStoredOffersHandler;
    private readonly ILogger<OfferCleanupFunction> _logger;

    public OfferCleanupFunction(
        DeleteExpiredFlightInventoryHandler deleteFlightInventoryHandler,
        DeleteExpiredStoredOffersHandler deleteStoredOffersHandler,
        ILogger<OfferCleanupFunction> logger)
    {
        _deleteFlightInventoryHandler = deleteFlightInventoryHandler;
        _deleteStoredOffersHandler = deleteStoredOffersHandler;
        _logger = logger;
    }

    // Runs once a day at midnight UTC: "0 0 0 * * *"
    [Function("DeleteExpiredFlightInventory")]
    public async Task RunDeleteExpiredFlightInventory(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredFlightInventory timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _deleteFlightInventoryHandler.HandleAsync(ct);
    }

    // Runs once a day at midnight UTC: "0 0 0 * * *"
    [Function("DeleteExpiredStoredOffers")]
    public async Task RunDeleteExpiredStoredOffers(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredStoredOffers timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _deleteStoredOffersHandler.HandleAsync(ct);
    }
}
