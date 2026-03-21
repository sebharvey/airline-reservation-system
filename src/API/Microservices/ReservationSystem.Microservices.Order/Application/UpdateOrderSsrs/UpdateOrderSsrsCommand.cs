namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;

/// <summary>
/// Command to update Special Service Requests (SSRs) on a confirmed order.
/// </summary>
public sealed record UpdateOrderSsrsCommand(string BookingReference, string SsrsData);
