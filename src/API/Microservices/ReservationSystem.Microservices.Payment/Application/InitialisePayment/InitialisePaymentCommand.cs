namespace ReservationSystem.Microservices.Payment.Application.InitialisePayment;

/// <summary>
/// Command carrying the data needed to initialise a new payment from order details.
/// ProductType is not set here — it is recorded per PaymentEvent at authorisation time.
/// </summary>
public sealed record InitialisePaymentCommand(
    string? BookingReference,
    string Method,
    string CurrencyCode,
    decimal Amount,
    string? Description);
