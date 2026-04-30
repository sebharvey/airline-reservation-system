namespace ReservationSystem.Microservices.Order.Application.GetOrderDebug;

/// <summary>
/// Query to retrieve a raw order row by booking reference — for debug use only.
/// </summary>
public sealed record GetOrderDebugQuery(string BookingReference);
