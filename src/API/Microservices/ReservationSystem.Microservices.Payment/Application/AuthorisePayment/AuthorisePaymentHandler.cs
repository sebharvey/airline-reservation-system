using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Handles the <see cref="AuthorisePaymentCommand"/>.
/// Derives CardType from BIN range, extracts CardLast4, creates the Payment
/// and a corresponding audit event, then persists both.
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

    public async Task<AuthorisePaymentResponse> HandleAsync(
        AuthorisePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var cardLast4 = command.CardNumber.Length >= 4
            ? command.CardNumber[^4..]
            : command.CardNumber;

        var cardType = DeriveCardType(command.CardNumber);
        var paymentReference = await GeneratePaymentReferenceAsync(cancellationToken);

        var payment = Domain.Entities.Payment.Create(
            paymentReference: paymentReference,
            bookingReference: null,
            paymentType: command.PaymentType,
            method: "CreditCard",
            cardType: cardType,
            cardLast4: cardLast4,
            currencyCode: command.CurrencyCode,
            authorisedAmount: command.Amount,
            description: command.Description);

        await _repository.CreateAsync(payment, cancellationToken);

        var paymentEvent = PaymentEvent.Create(
            payment.PaymentId,
            PaymentEventType.Authorise,
            command.Amount,
            command.CurrencyCode,
            $"Payment authorised for {command.PaymentType}");

        await _repository.CreateEventAsync(paymentEvent, cancellationToken);

        _logger.LogInformation("Authorised payment {PaymentReference} for {Amount} {Currency}",
            paymentReference, command.Amount, command.CurrencyCode);

        return PaymentMapper.ToAuthoriseResponse(payment);
    }

    private async Task<string> GeneratePaymentReferenceAsync(CancellationToken cancellationToken)
    {
        var sequence = await _repository.GetNextSequenceAsync(cancellationToken);
        return $"AXPAY-{sequence:D4}";
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
