namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

/// <summary>
/// Thrown when the Payment MS rejects card details with a 4xx response (e.g. Luhn
/// check failure, expired card, invalid CVV). Signals a client-correctable error
/// that the Retail API should surface as 422 rather than 500.
/// </summary>
public sealed class PaymentValidationException(string message) : Exception(message);
