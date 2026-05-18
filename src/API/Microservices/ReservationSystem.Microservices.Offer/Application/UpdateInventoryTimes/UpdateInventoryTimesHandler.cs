using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateInventoryTimes;

public sealed class UpdateInventoryTimesHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<UpdateInventoryTimesHandler> _logger;

    public UpdateInventoryTimesHandler(IOfferRepository repository, ILogger<UpdateInventoryTimesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpdateInventoryTimesCommand command, CancellationToken ct = default)
    {
        var departureDate        = DateOnly.ParseExact(command.DepartureDate, "yyyy-MM-dd");
        var newDepartureTime     = TimeOnly.ParseExact(command.NewDepartureTime, "HH:mm");
        var newArrivalTime       = TimeOnly.ParseExact(command.NewArrivalTime,   "HH:mm");
        TimeOnly? newDepUtc      = command.NewDepartureTimeUtc is not null ? TimeOnly.ParseExact(command.NewDepartureTimeUtc, "HH:mm") : null;
        TimeOnly? newArrUtc      = command.NewArrivalTimeUtc   is not null ? TimeOnly.ParseExact(command.NewArrivalTimeUtc,   "HH:mm") : null;

        var inventories = await _repository.GetInventoriesByFlightAsync(command.FlightNumber, departureDate, ct);

        if (inventories.Count == 0)
            throw new KeyNotFoundException($"No inventory found for {command.FlightNumber} on {command.DepartureDate}.");

        foreach (var inventory in inventories)
        {
            inventory.UpdateTimes(
                newDepartureTime,
                newArrivalTime,
                command.NewArrivalDayOffset,
                newDepUtc,
                newArrUtc,
                command.NewArrivalDayOffsetUtc);

            await _repository.UpdateInventoryAsync(inventory, ct);
        }

        _logger.LogInformation(
            "Updated times on {Count} inventories for {FlightNumber}/{DepartureDate}: dep={Dep} arr={Arr}",
            inventories.Count, command.FlightNumber, command.DepartureDate,
            command.NewDepartureTime, command.NewArrivalTime);

        return inventories.Count;
    }
}
