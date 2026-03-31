using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;

/// <summary>
/// Enriched flight inventory group with computed load factor and ticketing status.
/// </summary>
public sealed record FlightInventoryGroupResult(
    FlightInventoryGroup Group,
    int LoadFactor,
    string TicketingStatus);

public sealed class GetFlightInventoryByDateHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetFlightInventoryByDateHandler> _logger;

    public GetFlightInventoryByDateHandler(IOfferRepository repository, ILogger<GetFlightInventoryByDateHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightInventoryGroupResult>> HandleAsync(
        GetFlightInventoryByDateQuery query,
        CancellationToken ct = default)
    {
        var groups = await _repository.GetInventoryGroupedByDateAsync(query.DepartureDate, ct);

        _logger.LogInformation("Retrieved {Count} inventory groups for {DepartureDate}", groups.Count, query.DepartureDate);

        var now = DateTime.UtcNow;

        return groups.Select(g =>
        {
            var departure = g.DepartureDate.ToDateTime(g.DepartureTime, DateTimeKind.Utc);
            var ticketingStatus = (departure - now).TotalHours > 1 ? "Open" : "Closed";
            var loadFactor = g.TotalSeats > 0
                ? (int)Math.Round((double)(g.TotalSeats - g.TotalSeatsAvailable) / g.TotalSeats * 100)
                : 0;

            return new FlightInventoryGroupResult(g, loadFactor, ticketingStatus);
        }).ToList().AsReadOnly();
    }
}
