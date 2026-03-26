namespace ReservationSystem.Microservices.Payment.Application.GetPaymentEvents;

/// <summary>
/// Query to retrieve all payment event records for a given payment.
/// </summary>
public sealed record GetPaymentEventsQuery(Guid PaymentId);
