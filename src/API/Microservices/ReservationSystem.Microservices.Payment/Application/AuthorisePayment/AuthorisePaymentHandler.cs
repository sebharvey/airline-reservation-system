using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Handles the <see cref="AuthorisePaymentCommand"/>.
/// Derives CardType from BIN range, extracts CardLast4, authorises the payment
/// and creates a corresponding PaymentEvent, then persists both.
/// Full card number and CVV are discarded after processing (PCI DSS).
/// </summary>
public sealed class AuthorisePaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<AuthorisePaymentHandler> _logger;

    public AuthorisePaymentHandler(
        IPaymentRepository repository,
        ILogger<AuthorisePaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Authorises the payment identified by the command.
    /// Returns null when the paymentId does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be authorised.
    /// </summary>
    public async Task<AuthorisePaymentResponse?> HandleAsync(
        AuthorisePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Authorisation requested for unknown payment {PaymentId}", command.PaymentId);
            return null;
        }

        if (payment.Status != PaymentStatus.Initialised && payment.Status != PaymentStatus.Partial)
        {
            _logger.LogWarning("Cannot authorise payment {PaymentId} — current status is {Status}",
                command.PaymentId, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentId}' cannot be authorised — current status is '{payment.Status}'.");
        }

        // When no explicit amount is supplied, authorise the full payment amount.
        var amountToAuthorise = command.Amount ?? payment.Amount;

        if (amountToAuthorise <= 0)
            throw new ArgumentException("Amount to authorise must be greater than zero.");

        string? cardType;
        string? cardLast4;

        if (string.IsNullOrEmpty(command.CardNumber))
        {
            // Cash payments do not require card details — record the authorisation directly.
            if (!string.Equals(payment.Method, "Cash", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Card details are required for {payment.Method} payments.");

            cardType = null;
            cardLast4 = null;
        }
        else
        {
            if (!IsValidLuhn(command.CardNumber))
                throw new ArgumentException("Card number is invalid.");

            if (!IsValidExpiryDate(command.ExpiryDate))
                throw new ArgumentException("Card expiry date is invalid or in the past.");

            var isAmex = command.CardNumber.Length >= 2 && command.CardNumber[0] == '3'
                && (command.CardNumber[1] == '4' || command.CardNumber[1] == '7');
            var expectedCvvLength = isAmex ? 4 : 3;
            if (command.Cvv.Length != expectedCvvLength || !command.Cvv.All(char.IsDigit))
                throw new ArgumentException($"Security code must be {expectedCvvLength} digits.");

            cardLast4 = command.CardNumber.Length >= 4
                ? command.CardNumber[^4..]
                : command.CardNumber;

            cardType = DeriveCardType(command.CardNumber);

            // TODO: Call payment gateway (e.g. Adyen, Stripe, Worldpay) to authorise the card.
            // The gateway call should use the full card number, expiry, CVV, and amount held on
            // the Payment record. On success, persist the gateway authorisation code / token
            // (in memory only — never to the database). On decline, set Status = Declined and
            // return a 422 response. The gateway adapter will sit behind an IPaymentGateway interface.
        }

        payment.Authorise(amountToAuthorise, cardType, cardLast4);
        await _repository.UpdateAsync(payment, cancellationToken);

        var paymentEvent = PaymentEvent.Create(
            payment.PaymentId,
            PaymentEventType.Authorised,
            command.ProductType,
            amountToAuthorise,
            payment.CurrencyCode,
            $"{command.ProductType} authorised");

        await _repository.CreateEventAsync(paymentEvent, cancellationToken);

        _logger.LogInformation("Authorised payment {PaymentId} for {Amount} {Currency}",
            payment.PaymentId, payment.Amount, payment.CurrencyCode);

        return PaymentMapper.ToAuthoriseResponse(payment);
    }

    private static string DeriveCardType(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return "Unknown";

        return cardNumber[0] switch
        {
            '4' => "Visa",
            '5' => "Mastercard",
            '3' when cardNumber.Length >= 2 && (cardNumber[1] == '4' || cardNumber[1] == '7') => "Amex",
            _ => "Unknown"
        };
    }

    // -------------------------------------------------------------------------
    // Luhn algorithm — validates card number check digit
    // -------------------------------------------------------------------------

    private static bool IsValidLuhn(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = cardNumber.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(cardNumber[i]))
                return false;

            var digit = cardNumber[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    // -------------------------------------------------------------------------
    // Expiry date — validates MM/YY format and that the card has not expired
    // -------------------------------------------------------------------------

    private static bool IsValidExpiryDate(string expiryDate)
    {
        if (string.IsNullOrWhiteSpace(expiryDate))
            return false;

        var parts = expiryDate.Split('/');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year))
            return false;

        if (month < 1 || month > 12)
            return false;

        if (year < 100)
            year += 2000;

        var now = DateTime.UtcNow;

        // Card is valid through the end of the expiry month
        return year > now.Year || (year == now.Year && month >= now.Month);
    }
}
