using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SetInventoryOperationalData;

public sealed class SetInventoryOperationalDataHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SetInventoryOperationalDataHandler> _logger;

    public SetInventoryOperationalDataHandler(IOfferRepository repository, ILogger<SetInventoryOperationalDataHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(SetInventoryOperationalDataCommand command, CancellationToken ct = default)
    {
        var inventory = await _repository.GetInventoryByIdAsync(command.InventoryId, ct)
            ?? throw new KeyNotFoundException($"No inventory found for id {command.InventoryId}.");

        inventory.SetDepartureGate(command.DepartureGate);
        inventory.SetAircraftRegistration(command.AircraftRegistration);

        await _repository.UpdateInventoryOperationalDataAsync(command.InventoryId, command.DepartureGate, command.AircraftRegistration, ct);

        _logger.LogInformation(
            "Set operational data for inventory {InventoryId}: gate={Gate}, reg={Reg}",
            command.InventoryId, command.DepartureGate, command.AircraftRegistration);
    }
}
