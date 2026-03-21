namespace ReservationSystem.Microservices.Order.Application.GetOrder;

/// <summary>
/// Query to retrieve an order by its 6-character booking reference.
/// </summary>
public sealed record GetOrderQuery(string BookingReference);
