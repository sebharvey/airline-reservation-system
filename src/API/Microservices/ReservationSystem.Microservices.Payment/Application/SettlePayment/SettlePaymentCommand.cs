namespace ReservationSystem.Microservices.Payment.Application.SettlePayment;

/// <summary>
/// Command carrying the data needed to settle an authorised payment.
/// </summary>
public sealed record SettlePaymentCommand(
    string PaymentReference,
    decimal Amount);
