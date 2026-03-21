using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.RefundPayment;

/// <summary>
/// Handles the <see cref="RefundPaymentCommand"/>.
/// Refunds a previously settled payment (full or partial).
/// </summary>
public sealed class RefundPaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<RefundPaymentHandler> _logger;

    public RefundPaymentHandler(
        IPaymentRepository repository,
        ILogger<RefundPaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Refunds the payment identified by the command.
    /// Returns null when the payment reference does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be refunded
    /// (wrong status or amount exceeds settled amount).
    /// </summary>
    public async Task<RefundPaymentResponse?> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByReferenceAsync(command.PaymentReference, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Refund requested for unknown payment {PaymentReference}", command.PaymentReference);
            return null;
        }

        if (payment.Status != PaymentStatus.Settled && payment.Status != PaymentStatus.PartiallySettled)
        {
            _logger.LogWarning("Cannot refund payment {PaymentReference} — current status is {Status}",
                command.PaymentReference, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentReference}' cannot be refunded — current status is '{payment.Status}'.");
        }

        if (command.Amount > payment.SettledAmount)
        {
            throw new ArgumentException(
                $"Refund amount ({command.Amount}) exceeds settled amount ({payment.SettledAmount}).");
        }

        payment.Refund(command.Amount);
        await _repository.UpdateAsync(payment, cancellationToken);

        var paymentEvent = PaymentEvent.Create(
            payment.PaymentId,
            PaymentEventType.Refunded,
            command.Amount,
            payment.CurrencyCode,
            $"Refund reason: {command.Reason}");

        await _repository.CreateEventAsync(paymentEvent, cancellationToken);

        _logger.LogInformation("Refunded payment {PaymentReference} for {Amount} {Currency} — reason: {Reason}",
            command.PaymentReference, command.Amount, payment.CurrencyCode, command.Reason);

        return PaymentMapper.ToRefundResponse(payment, command.Amount);
    }
}
