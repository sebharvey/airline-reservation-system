using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.GetSeatAvailability;

public sealed record SeatAvailabilityItem(string SeatOfferId, string SeatNumber, string Status);

public sealed class GetSeatAvailabilityHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<GetSeatAvailabilityHandler> _logger;

    public GetSeatAvailabilityHandler(IOfferRepository repository, ILogger<GetSeatAvailabilityHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<(FlightInventory Inventory, IReadOnlyList<SeatAvailabilityItem> Seats)?> HandleAsync(
        GetSeatAvailabilityQuery query, CancellationToken ct = default)
    {
        var inventory = await _repository.GetInventoryByIdAsync(query.FlightId, ct);
        if (inventory is null)
            return null;

        var reservations = await _repository.GetSeatReservationsAsync(query.FlightId, ct);

        var seats = reservations.Select(r => new SeatAvailabilityItem(
            SeatOfferId: $"so-{query.FlightId:N}-{r.SeatNumber}-v1",
            SeatNumber: r.SeatNumber,
            Status: r.Status == "held" ? "held" : r.Status == "sold" || r.Status == "checked-in" ? "sold" : "available"
        )).ToList().AsReadOnly();

        return (inventory, seats);
    }
}
