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
        var inventories = command.Items
            .Select(item => FlightInventory.Create(
                item.FlightNumber,
                DateOnly.Parse(item.DepartureDate),
                TimeOnly.Parse(item.DepartureTime),
                TimeOnly.Parse(item.ArrivalTime),
                item.ArrivalDayOffset,
                item.Origin, item.Destination,
                item.AircraftType, item.CabinCode, item.TotalSeats))
            .ToList()
            .AsReadOnly();

        var created = await _repository.BatchCreateInventoryAsync(inventories, ct);
        var skippedCount = command.Items.Count - created.Count;

        _logger.LogInformation(
            "BatchCreateFlights: created {Created}, skipped {Skipped} existing records",
            created.Count, skippedCount);

        return new BatchCreateFlightsResult(created, skippedCount);
    }
}

public sealed record BatchCreateFlightsResult(
    IReadOnlyList<FlightInventory> Created,
    int SkippedCount);
