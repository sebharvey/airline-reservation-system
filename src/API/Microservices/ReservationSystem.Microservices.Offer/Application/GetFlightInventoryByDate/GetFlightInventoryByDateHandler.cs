using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;

/// <summary>
/// Enriched flight inventory group with computed load factor and effective status.
/// </summary>
public sealed record FlightInventoryGroupResult(
    FlightInventoryGroup Group,
    int LoadFactor,
    string EffectiveStatus);

public sealed record FlightInventoryWithPinnedResult(
    IReadOnlyList<FlightInventoryGroupResult> Flights,
    IReadOnlyList<FlightInventoryGroupResult> PinnedFlights);

public sealed class GetFlightInventoryByDateHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetFlightInventoryByDateHandler> _logger;

    public GetFlightInventoryByDateHandler(IOfferRepository repository, ILogger<GetFlightInventoryByDateHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FlightInventoryWithPinnedResult> HandleAsync(
        GetFlightInventoryByDateQuery query,
        CancellationToken ct = default)
    {
        var pinnedIds = query.PinnedInventoryIds ?? [];

        var groupsTask = _repository.GetInventoryGroupedByDateAsync(query.DepartureDate, ct);
        var pinnedTask = pinnedIds.Count > 0
            ? _repository.GetInventoryGroupedByIdsAsync(pinnedIds, ct)
            : Task.FromResult<IReadOnlyList<FlightInventoryGroup>>([]);

        await Task.WhenAll(groupsTask, pinnedTask);

        var groups = groupsTask.Result;
        var pinnedGroups = pinnedTask.Result;

        _logger.LogInformation(
            "Retrieved {Count} inventory groups for {DepartureDate}, {PinnedCount} pinned",
            groups.Count, query.DepartureDate, pinnedGroups.Count);

        var now = DateTime.UtcNow;
        var pinnedIdSet = new HashSet<Guid>(pinnedIds);

        var flights = groups
            .Where(g => !pinnedIdSet.Contains(g.InventoryId))
            .Select(g => Enrich(g, now))
            .ToList()
            .AsReadOnly();

        var pinned = pinnedGroups
            .Select(g => Enrich(g, now))
            .ToList()
            .AsReadOnly();

        return new FlightInventoryWithPinnedResult(flights, pinned);
    }

    private static FlightInventoryGroupResult Enrich(FlightInventoryGroup g, DateTime now)
    {
        var departure = g.DepartureDate.ToDateTime(g.DepartureTime, DateTimeKind.Utc);
        var soldSeats = (g.F?.SeatsSold ?? 0) + (g.J?.SeatsSold ?? 0) + (g.W?.SeatsSold ?? 0) + (g.Y?.SeatsSold ?? 0);
        var loadFactor = g.TotalSeats > 0
            ? (int)Math.Round((double)soldSeats / g.TotalSeats * 100)
            : 0;
        var effectiveStatus = g.Status == "Active" && (departure - now).TotalHours <= 1
            ? "Ticketing Closed"
            : g.Status;
        return new FlightInventoryGroupResult(g, loadFactor, effectiveStatus);
    }
}
