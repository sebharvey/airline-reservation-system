namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Command carrying the data needed to authorise a new payment.
/// Immutable record — the application layer maps HTTP request models to this
/// before passing it to the handler, keeping the handler free of HTTP concerns.
/// </summary>
public sealed record AuthorisePaymentCommand(
    string? BookingReference,
    string PaymentType,
    string Method,
    string? CardType,
    string? CardLast4,
    string CurrencyCode,
    decimal Amount,
    string? Description);
