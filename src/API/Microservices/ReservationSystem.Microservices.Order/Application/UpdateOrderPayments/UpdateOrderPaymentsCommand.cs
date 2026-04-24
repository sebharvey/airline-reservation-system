namespace ReservationSystem.Microservices.Order.Application.UpdateOrderPayments;

/// <summary>
/// Command to append one or more payment records to a confirmed order's payment history.
/// </summary>
public sealed record UpdateOrderPaymentsCommand(string BookingReference, string PaymentsData);
