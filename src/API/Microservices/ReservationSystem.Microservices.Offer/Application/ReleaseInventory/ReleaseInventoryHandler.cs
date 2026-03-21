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

        if (command.ReleaseType == "Held")
        {
            if (inventory.SeatsHeld < command.PaxCount)
                throw new InvalidOperationException($"Insufficient held seats: {inventory.SeatsHeld} held, {command.PaxCount} requested.");
            inventory.ReleaseHeld(command.PaxCount);
        }
        else if (command.ReleaseType == "Sold")
        {
            if (inventory.SeatsSold < command.PaxCount)
                throw new InvalidOperationException($"Insufficient sold seats: {inventory.SeatsSold} sold, {command.PaxCount} requested.");
            inventory.ReleaseSold(command.PaxCount);
        }
        else
        {
            throw new ArgumentException($"Invalid releaseType: {command.ReleaseType}. Must be 'Held' or 'Sold'.");
        }

        await _repository.UpdateInventoryAsync(inventory, ct);

        _logger.LogInformation("Released {PaxCount} {ReleaseType} seats on inventory {InventoryId}",
            command.PaxCount, command.ReleaseType, command.InventoryId);

        return inventory;
    }
}
