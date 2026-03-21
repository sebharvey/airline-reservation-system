namespace ReservationSystem.Microservices.Payment.Application.RefundPayment;

/// <summary>
/// Command carrying the data needed to refund a settled payment.
/// </summary>
public sealed record RefundPaymentCommand(
    string PaymentReference,
    decimal Amount,
    string Reason);
