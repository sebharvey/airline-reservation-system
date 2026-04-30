namespace ReservationSystem.Microservices.Order.Application.GetIropsOrdersByIds;

/// <summary>
/// Query to batch-fetch specific orders projected for IROPS processing, identified by OrderIds.
/// </summary>
public sealed record GetIropsOrdersByIdsQuery(IReadOnlyList<Guid> OrderIds, string FlightNumber, string DepartureDate);
