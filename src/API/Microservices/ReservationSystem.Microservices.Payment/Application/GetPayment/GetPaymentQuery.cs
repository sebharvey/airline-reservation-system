namespace ReservationSystem.Microservices.Payment.Application.GetPayment;

/// <summary>
/// Query to retrieve a payment record by its identifier.
/// </summary>
public sealed record GetPaymentQuery(Guid PaymentId);
