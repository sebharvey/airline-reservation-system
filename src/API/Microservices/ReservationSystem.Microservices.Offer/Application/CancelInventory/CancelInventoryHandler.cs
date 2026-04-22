using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.CancelInventory;

public sealed class CancelInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CancelInventoryHandler> _logger;

    public CancelInventoryHandler(IOfferRepository repository, ILogger<CancelInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CancelInventoryCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);

        var inventories = await _repository.GetInventoriesByFlightAsync(
            command.FlightNumber, departureDate, ct);

        if (inventories.Count == 0)
            throw new KeyNotFoundException($"No active inventory found for {command.FlightNumber} on {command.DepartureDate}.");

        var alreadyCancelled = inventories.All(i => i.Status == Domain.Entities.InventoryStatus.Cancelled);
        if (alreadyCancelled)
            return 0;

        var cancelledCount = 0;
        foreach (var inventory in inventories)
        {
            if (inventory.Status == Domain.Entities.InventoryStatus.Cancelled)
                continue;

            inventory.Cancel();
            await _repository.UpdateInventoryAsync(inventory, ct);
            cancelledCount++;
        }

        _logger.LogInformation("Cancelled {Count} inventories for flight {FlightNumber} on {Date}",
            cancelledCount, command.FlightNumber, command.DepartureDate);

        return cancelledCount;
    }
}
