namespace ReservationSystem.Microservices.Order.Application.RetrieveOrder;

/// <summary>
/// Query to retrieve an order by booking reference and validate a passenger surname match.
/// </summary>
public sealed record RetrieveOrderQuery(string BookingReference, string Surname);
