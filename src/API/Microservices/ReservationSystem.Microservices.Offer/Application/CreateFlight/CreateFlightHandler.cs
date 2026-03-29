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

        var existing = await _repository.GetInventoryAsync(command.FlightNumber, departureDate, ct);

        if (existing is not null)
            throw new InvalidOperationException($"Inventory already exists for {command.FlightNumber} on {command.DepartureDate}.");

        var cabins = command.Cabins.Select(c => (c.CabinCode, c.TotalSeats)).ToList().AsReadOnly();

        var inventory = FlightInventory.Create(
            command.FlightNumber, departureDate, departureTime, arrivalTime,
            command.ArrivalDayOffset, command.Origin, command.Destination,
            command.AircraftType, cabins);

        await _repository.CreateInventoryAsync(inventory, ct);

        _logger.LogInformation("Created FlightInventory {InventoryId} for {FlightNumber} on {Date} with {CabinCount} cabins",
            inventory.InventoryId, command.FlightNumber, command.DepartureDate, command.Cabins.Count);

        return inventory;
    }
}
