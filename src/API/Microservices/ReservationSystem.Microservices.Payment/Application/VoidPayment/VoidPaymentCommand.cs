namespace ReservationSystem.Microservices.Payment.Application.VoidPayment;

/// <summary>
/// Command carrying the data needed to void an authorised payment.
/// </summary>
public sealed record VoidPaymentCommand(
    Guid PaymentId,
    string? Reason);
