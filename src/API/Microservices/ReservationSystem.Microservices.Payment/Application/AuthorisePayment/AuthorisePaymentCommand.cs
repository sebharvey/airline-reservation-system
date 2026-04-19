namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Command carrying the data needed to authorise an initialised payment.
/// Card number and CVV are held in memory only for authorisation processing
/// and are never persisted (PCI DSS compliance).
/// <para>
/// <see cref="Amount"/> is optional. When null the handler derives the amount from
/// the remaining uninitialised balance on the payment (full-amount authorisation).
/// When provided it enables partial authorisation, allowing multiple auth+settle
/// cycles against a single initialised payment.
/// </para>
/// <para>
/// <see cref="ProductType"/> identifies the product being paid for (e.g. Fare, Seat, Bag)
/// and is recorded on the resulting PaymentEvent row.
/// </para>
/// </summary>
public sealed record AuthorisePaymentCommand(
    Guid PaymentId,
    string ProductType,
    decimal? Amount,
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName);
