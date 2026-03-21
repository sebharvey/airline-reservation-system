using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SellInventory;

public sealed class SellInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SellInventoryHandler> _logger;

    public SellInventoryHandler(IOfferRepository repository, ILogger<SellInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightInventory>> HandleAsync(SellInventoryCommand command, CancellationToken ct = default)
    {
        var results = new List<FlightInventory>();

        foreach (var inventoryId in command.InventoryIds)
        {
            var inventory = await _repository.GetInventoryByIdAsync(inventoryId, ct)
                ?? throw new KeyNotFoundException($"Inventory {inventoryId} not found.");

            if (inventory.SeatsHeld < command.PaxCount)
                throw new InvalidOperationException($"Insufficient held seats on {inventoryId}: {inventory.SeatsHeld} held, {command.PaxCount} requested.");

            inventory.SellSeats(command.PaxCount);
            await _repository.UpdateInventoryAsync(inventory, ct);
            results.Add(inventory);
        }

        _logger.LogInformation("Sold {PaxCount} seats across {Count} inventories for basket {BasketId}",
            command.PaxCount, command.InventoryIds.Count, command.BasketId);

        return results.AsReadOnly();
    }
}
