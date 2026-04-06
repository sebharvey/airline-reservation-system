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

        foreach (var item in command.Items)
        {
            var holdCount = await _repository.GetHoldCountAsync(item.InventoryId, command.OrderId, item.CabinCode, ct);

            var inventory = await _repository.GetInventoryByIdAsync(item.InventoryId, ct)
                ?? throw new KeyNotFoundException($"Inventory {item.InventoryId} not found.");

            var cabin = inventory.Cabins.FirstOrDefault(c => c.CabinCode == item.CabinCode)
                ?? throw new ArgumentException($"Cabin {item.CabinCode} not found on inventory {item.InventoryId}.");

            if (cabin.SeatsHeld < holdCount)
                throw new InvalidOperationException($"Insufficient held seats in cabin {item.CabinCode} on {item.InventoryId}: {cabin.SeatsHeld} held, {holdCount} required.");

            inventory.SellSeats(item.CabinCode, holdCount);
            await _repository.UpdateInventoryAsync(inventory, ct);
            await _repository.ConfirmHoldAsync(item.InventoryId, command.OrderId, item.CabinCode, ct);
            results.Add(inventory);
        }

        _logger.LogInformation("Sold seats across {Count} inventories for order {OrderId}",
            command.Items.Count, command.OrderId);

        return results.AsReadOnly();
    }
}
