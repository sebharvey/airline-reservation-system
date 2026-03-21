namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Command carrying the data needed to authorise a new payment.
/// Card number and CVV are held in memory only for authorisation processing
/// and are never persisted (PCI DSS compliance).
/// </summary>
public sealed record AuthorisePaymentCommand(
    decimal Amount,
    string CurrencyCode,
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName,
    string PaymentType,
    string? Description);
