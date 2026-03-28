using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;

/// <summary>
/// Creates flight inventory records in bulk, skipping any that already exist.
/// Called by the Operations API when importing schedules into inventory.
/// </summary>
public sealed class BatchCreateFlightsHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<BatchCreateFlightsHandler> _logger;

    public BatchCreateFlightsHandler(IOfferRepository repository, ILogger<BatchCreateFlightsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BatchCreateFlightsResult> HandleAsync(
        BatchCreateFlightsCommand command, CancellationToken ct = default)
    {
        var created = new List<FlightInventory>();
        var skippedCount = 0;

        foreach (var item in command.Items)
        {
            var departureDate = DateOnly.Parse(item.DepartureDate);

            var existing = await _repository.GetInventoryAsync(
                item.FlightNumber, departureDate, item.CabinCode, ct);

            if (existing is not null)
            {
                skippedCount++;
                continue;
            }

            var departureTime = TimeOnly.Parse(item.DepartureTime);
            var arrivalTime = TimeOnly.Parse(item.ArrivalTime);

            var inventory = FlightInventory.Create(
                item.FlightNumber, departureDate, departureTime, arrivalTime,
                item.ArrivalDayOffset, item.Origin, item.Destination,
                item.AircraftType, item.CabinCode, item.TotalSeats);

            await _repository.CreateInventoryAsync(inventory, ct);
            created.Add(inventory);
        }

        _logger.LogInformation(
            "BatchCreateFlights: created {Created}, skipped {Skipped} existing records",
            created.Count, skippedCount);

        return new BatchCreateFlightsResult(created.AsReadOnly(), skippedCount);
    }
}

public sealed record BatchCreateFlightsResult(
    IReadOnlyList<FlightInventory> Created,
    int SkippedCount);
