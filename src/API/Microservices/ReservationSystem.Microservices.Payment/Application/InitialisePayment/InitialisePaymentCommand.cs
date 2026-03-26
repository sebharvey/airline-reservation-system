namespace ReservationSystem.Microservices.Payment.Application.InitialisePayment;

/// <summary>
/// Command carrying the data needed to initialise a new payment from order details.
/// </summary>
public sealed record InitialisePaymentCommand(
    string? BookingReference,
    string PaymentType,
    string Method,
    string CurrencyCode,
    decimal Amount,
    string? Description);
