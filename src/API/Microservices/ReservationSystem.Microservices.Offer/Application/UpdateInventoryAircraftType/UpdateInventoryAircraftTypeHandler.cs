using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateInventoryAircraftType;

public sealed class UpdateInventoryAircraftTypeHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<UpdateInventoryAircraftTypeHandler> _logger;

    public UpdateInventoryAircraftTypeHandler(IOfferRepository repository, ILogger<UpdateInventoryAircraftTypeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpdateInventoryAircraftTypeCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);

        var inventories = await _repository.GetInventoriesByFlightAsync(
            command.FlightNumber, departureDate, ct);

        if (inventories.Count == 0)
            throw new KeyNotFoundException($"No inventory found for {command.FlightNumber} on {command.DepartureDate}.");

        var updatedCount = 0;
        foreach (var inventory in inventories)
        {
            inventory.ChangeAircraftType(command.NewAircraftType);
            await _repository.UpdateInventoryAsync(inventory, ct);
            updatedCount++;
        }

        _logger.LogInformation(
            "Updated aircraft type to {NewType} for {Count} inventories on flight {FlightNumber} on {Date}",
            command.NewAircraftType, updatedCount, command.FlightNumber, command.DepartureDate);

        return updatedCount;
    }
}
