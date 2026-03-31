using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.DeleteExpiredDraftOrders;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class OrderCleanupFunction
{
    private readonly DeleteExpiredDraftOrdersHandler _handler;
    private readonly ILogger<OrderCleanupFunction> _logger;

    public OrderCleanupFunction(
        DeleteExpiredDraftOrdersHandler handler,
        ILogger<OrderCleanupFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // Runs daily at midnight UTC: "0 0 0 * * *"
    [Function("DeleteExpiredDraftOrders")]
    public async Task Run(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("DeleteExpiredDraftOrders timer triggered at {UtcNow:O}", DateTime.UtcNow);

        await _handler.HandleAsync(ct);
    }
}
