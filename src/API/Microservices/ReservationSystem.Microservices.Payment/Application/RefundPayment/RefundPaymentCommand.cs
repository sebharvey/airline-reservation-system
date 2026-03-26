namespace ReservationSystem.Microservices.Payment.Application.RefundPayment;

/// <summary>
/// Command carrying the data needed to refund a settled payment.
/// </summary>
public sealed record RefundPaymentCommand(
    Guid PaymentId,
    decimal Amount,
    string Reason);
