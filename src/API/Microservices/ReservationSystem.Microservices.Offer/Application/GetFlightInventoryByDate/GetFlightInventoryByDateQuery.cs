namespace ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;

public sealed record GetFlightInventoryByDateQuery(
    DateOnly DepartureDate,
    IReadOnlyList<Guid>? PinnedInventoryIds = null);
