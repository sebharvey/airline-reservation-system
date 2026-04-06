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
            var inventory = await _repository.GetInventoryByIdAsync(item.InventoryId, ct)
                ?? throw new KeyNotFoundException($"Inventory {item.InventoryId} not found.");

            var cabin = inventory.Cabins.FirstOrDefault(c => c.CabinCode == item.CabinCode)
                ?? throw new ArgumentException($"Cabin {item.CabinCode} not found on inventory {item.InventoryId}.");

            if (cabin.SeatsHeld < command.PaxCount)
                throw new InvalidOperationException($"Insufficient held seats in cabin {item.CabinCode} on {item.InventoryId}: {cabin.SeatsHeld} held, {command.PaxCount} requested.");

            inventory.SellSeats(item.CabinCode, command.PaxCount);
            await _repository.UpdateInventoryAsync(inventory, ct);
            await _repository.ConfirmHoldAsync(item.InventoryId, command.OrderId, ct);
            results.Add(inventory);
        }

        _logger.LogInformation("Sold {PaxCount} seats across {Count} inventories for order {OrderId}",
            command.PaxCount, command.Items.Count, command.OrderId);

        return results.AsReadOnly();
    }
}
