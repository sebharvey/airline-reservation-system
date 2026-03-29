using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.ReleaseInventory;

public sealed class ReleaseInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<ReleaseInventoryHandler> _logger;

    public ReleaseInventoryHandler(IOfferRepository repository, ILogger<ReleaseInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightInventory> HandleAsync(ReleaseInventoryCommand command, CancellationToken ct = default)
    {
        var inventory = await _repository.GetInventoryByIdAsync(command.InventoryId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.InventoryId} not found.");

        var cabin = inventory.Cabins.FirstOrDefault(c => c.CabinCode == command.CabinCode)
            ?? throw new ArgumentException($"Cabin {command.CabinCode} not found on inventory {command.InventoryId}.");

        if (command.ReleaseType == "Held")
        {
            if (cabin.SeatsHeld < command.PaxCount)
                throw new InvalidOperationException($"Insufficient held seats in cabin {command.CabinCode}: {cabin.SeatsHeld} held, {command.PaxCount} requested.");
            inventory.ReleaseHeld(command.CabinCode, command.PaxCount);
        }
        else if (command.ReleaseType == "Sold")
        {
            if (cabin.SeatsSold < command.PaxCount)
                throw new InvalidOperationException($"Insufficient sold seats in cabin {command.CabinCode}: {cabin.SeatsSold} sold, {command.PaxCount} requested.");
            inventory.ReleaseSold(command.CabinCode, command.PaxCount);
        }
        else
        {
            throw new ArgumentException($"Invalid releaseType: {command.ReleaseType}. Must be 'Held' or 'Sold'.");
        }

        await _repository.UpdateInventoryAsync(inventory, ct);

        _logger.LogInformation("Released {PaxCount} {ReleaseType} seats in cabin {CabinCode} on inventory {InventoryId}",
            command.PaxCount, command.ReleaseType, command.CabinCode, command.InventoryId);

        return inventory;
    }
}
