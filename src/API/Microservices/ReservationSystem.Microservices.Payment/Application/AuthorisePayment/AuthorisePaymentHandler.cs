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
            cardLast4 = command.CardNumber.Length >= 4
                ? command.CardNumber[^4..]
                : command.CardNumber;

            cardType = DeriveCardType(command.CardNumber);

            // KNOWN GAP (arch-review C-03): Payment gateway integration is not implemented.
            // This is intentional for the current POC phase — the authorisation flow records
            // state transitions correctly but does not move money.
            //
            // Before production: select a processor (Adyen, Stripe, or Worldpay), implement
            // IPaymentGatewayClient, wire it into this handler, and add integration tests
            // covering success, decline, and 3DS challenge flows.
            // On decline the handler must set Status = Declined and return a 422 response.
            // The full card number, expiry, and CVV must never be persisted to the database.
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
}
