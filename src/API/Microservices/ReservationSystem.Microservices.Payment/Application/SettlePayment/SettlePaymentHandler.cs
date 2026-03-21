using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.SettlePayment;

/// <summary>
/// Handles the <see cref="SettlePaymentCommand"/>.
/// Settles a previously authorised payment by capturing the funds.
/// </summary>
public sealed class SettlePaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<SettlePaymentHandler> _logger;

    public SettlePaymentHandler(
        IPaymentRepository repository,
        ILogger<SettlePaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Settles the payment identified by the command.
    /// Returns null when the payment reference does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be settled
    /// (wrong status or amount exceeds authorised amount).
    /// </summary>
    public async Task<SettlePaymentResponse?> HandleAsync(
        SettlePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByReferenceAsync(command.PaymentReference, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Settlement requested for unknown payment {PaymentReference}", command.PaymentReference);
            return null;
        }

        if (payment.Status != PaymentStatus.Authorised)
        {
            _logger.LogWarning("Cannot settle payment {PaymentReference} — current status is {Status}",
                command.PaymentReference, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentReference}' cannot be settled — current status is '{payment.Status}'.");
        }

        if (command.Amount > payment.AuthorisedAmount)
        {
            throw new ArgumentException(
                $"Settled amount ({command.Amount}) exceeds authorised amount ({payment.AuthorisedAmount}).");
        }

        payment.Settle(command.Amount);
        await _repository.UpdateAsync(payment, cancellationToken);

        var paymentEvent = PaymentEvent.Create(
            payment.PaymentId,
            PaymentEventType.Settled,
            command.Amount,
            payment.CurrencyCode,
            $"Payment settled for {command.Amount} {payment.CurrencyCode}");

        await _repository.CreateEventAsync(paymentEvent, cancellationToken);

        _logger.LogInformation("Settled payment {PaymentReference} for {Amount} {Currency}",
            command.PaymentReference, command.Amount, payment.CurrencyCode);

        return PaymentMapper.ToSettleResponse(payment);
    }
}
