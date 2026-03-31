using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventory;

/// <summary>
/// Aggregated flight inventory result for a specific flight number and departure date.
/// </summary>
public sealed record GetFlightInventoryResult(
    FlightInventory First,
    int TotalSeats,
    int TotalAvailable,
    IReadOnlyDictionary<string, CabinAggregation> CabinAggregations);

public sealed record CabinAggregation(int TotalSeats, int SeatsAvailable, int SeatsSold, int SeatsHeld);

public sealed class GetFlightInventoryHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetFlightInventoryHandler> _logger;

    public GetFlightInventoryHandler(IOfferRepository repository, ILogger<GetFlightInventoryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<GetFlightInventoryResult?> HandleAsync(
        GetFlightInventoryQuery query,
        CancellationToken ct = default)
    {
        var inventories = await _repository.GetInventoriesByFlightAsync(query.FlightNumber, query.DepartureDate, ct);

        if (inventories.Count == 0)
        {
            _logger.LogInformation(
                "No inventory found for flight {FlightNumber} on {DepartureDate}",
                query.FlightNumber, query.DepartureDate);
            return null;
        }

        var totalSeats = inventories.Sum(i => i.TotalSeats);
        var totalAvailable = inventories.Sum(i => i.SeatsAvailable);

        var cabinAggregations = inventories
            .SelectMany(i => i.Cabins)
            .GroupBy(c => c.CabinCode)
            .ToDictionary(
                g => g.Key,
                g => new CabinAggregation(
                    TotalSeats: g.Sum(c => c.TotalSeats),
                    SeatsAvailable: g.Sum(c => c.SeatsAvailable),
                    SeatsSold: g.Sum(c => c.SeatsSold),
                    SeatsHeld: g.Sum(c => c.SeatsHeld)));

        return new GetFlightInventoryResult(inventories[0], totalSeats, totalAvailable, cabinAggregations);
    }
}
