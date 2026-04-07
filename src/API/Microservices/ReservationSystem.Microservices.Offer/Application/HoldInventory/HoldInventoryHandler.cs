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
        var requestedCount = command.Passengers.Count;

        var existingCount = await _repository.GetHoldCountAsync(command.InventoryId, command.OrderId, command.CabinCode, ct);
        if (existingCount == requestedCount)
        {
            var existing = await _repository.GetInventoryByIdAsync(command.InventoryId, ct);
            return existing!;
        }

        var inventory = await _repository.GetInventoryByIdAsync(command.InventoryId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.InventoryId} not found.");

        var cabin = inventory.Cabins.FirstOrDefault(c => c.CabinCode == command.CabinCode)
            ?? throw new ArgumentException($"Cabin {command.CabinCode} not found on inventory {command.InventoryId}.");

        if (cabin.SeatsAvailable < requestedCount)
            throw new InvalidOperationException($"Insufficient seats in cabin {command.CabinCode}: {cabin.SeatsAvailable} available, {requestedCount} requested.");

        inventory.HoldSeats(command.CabinCode, requestedCount);
        await _repository.UpdateInventoryAsync(inventory, ct);

        foreach (var pax in command.Passengers)
            await _repository.CreateHoldAsync(command.InventoryId, command.OrderId, command.CabinCode, pax.SeatNumber, pax.PassengerId, ct);

        _logger.LogInformation("Held {PaxCount} seats on inventory {InventoryId} for order {OrderId}",
            requestedCount, command.InventoryId, command.OrderId);

        return inventory;
    }
}
