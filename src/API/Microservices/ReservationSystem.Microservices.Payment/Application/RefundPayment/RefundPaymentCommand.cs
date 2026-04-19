namespace ReservationSystem.Microservices.Payment.Application.RefundPayment;

/// <summary>
/// Command carrying the data needed to refund a settled payment.
/// <see cref="ProductType"/> is optional; when null the handler derives it from
/// the most recent Settled event on the same payment.
/// </summary>
public sealed record RefundPaymentCommand(
    Guid PaymentId,
    decimal Amount,
    string Reason,
    string? ProductType = null);
