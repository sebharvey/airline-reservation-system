using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.HoldInventory;

public sealed class HoldInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<HoldInventoryHandler> _logger;

    public HoldInventoryHandler(IOfferRepository repository, ILogger<HoldInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightInventory> HandleAsync(HoldInventoryCommand command, CancellationToken ct = default)
    {
        var holdExists = await _repository.HoldExistsAsync(command.InventoryId, command.BasketId, ct);
        if (holdExists)
        {
            var existing = await _repository.GetInventoryByIdAsync(command.InventoryId, ct);
            return existing!;
        }

        var inventory = await _repository.GetInventoryByIdAsync(command.InventoryId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.InventoryId} not found.");

        if (inventory.CabinCode != command.CabinCode)
            throw new ArgumentException($"CabinCode mismatch: expected {inventory.CabinCode}, got {command.CabinCode}.");

        if (inventory.SeatsAvailable < command.PaxCount)
            throw new InvalidOperationException($"Insufficient seats: {inventory.SeatsAvailable} available, {command.PaxCount} requested.");

        inventory.HoldSeats(command.PaxCount);
        await _repository.UpdateInventoryAsync(inventory, ct);
        await _repository.CreateHoldAsync(command.InventoryId, command.BasketId, command.PaxCount, ct);

        _logger.LogInformation("Held {PaxCount} seats on inventory {InventoryId} for basket {BasketId}",
            command.PaxCount, command.InventoryId, command.BasketId);

        return inventory;
    }
}
