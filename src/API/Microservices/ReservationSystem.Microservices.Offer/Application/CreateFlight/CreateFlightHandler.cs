using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.CreateFlight;

public sealed class CreateFlightHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CreateFlightHandler> _logger;

    public CreateFlightHandler(IOfferRepository repository, ILogger<CreateFlightHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightInventory> HandleAsync(CreateFlightCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);
        var departureTime = TimeOnly.Parse(command.DepartureTime);
        var arrivalTime = TimeOnly.Parse(command.ArrivalTime);

        var existing = await _repository.GetInventoryAsync(
            command.FlightNumber, departureDate, command.CabinCode, ct);

        if (existing is not null)
            throw new InvalidOperationException($"Inventory already exists for {command.FlightNumber} on {command.DepartureDate} cabin {command.CabinCode}.");

        var inventory = FlightInventory.Create(
            command.FlightNumber, departureDate, departureTime, arrivalTime,
            command.ArrivalDayOffset, command.Origin, command.Destination,
            command.AircraftType, command.CabinCode, command.TotalSeats);

        await _repository.CreateInventoryAsync(inventory, ct);

        _logger.LogInformation("Created FlightInventory {InventoryId} for {FlightNumber} on {Date} cabin {Cabin}",
            inventory.InventoryId, command.FlightNumber, command.DepartureDate, command.CabinCode);

        return inventory;
    }
}
