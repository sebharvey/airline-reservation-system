namespace ReservationSystem.Microservices.Order.Application.GetOrderBookingReferences;

/// <summary>
/// Query to batch-resolve booking references for a list of order IDs.
/// </summary>
public sealed record GetOrderBookingReferencesQuery(IReadOnlyList<Guid> OrderIds);
