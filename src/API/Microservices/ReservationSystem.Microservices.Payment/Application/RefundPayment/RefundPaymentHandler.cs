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
    /// Returns null when the paymentId does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be refunded.
    /// </summary>
    public async Task<RefundPaymentResponse?> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Refund requested for unknown payment {PaymentId}", command.PaymentId);
            return null;
        }

        if (payment.Status != PaymentStatus.Settled && payment.Status != PaymentStatus.PartiallySettled)
        {
            _logger.LogWarning("Cannot refund payment {PaymentId} — current status is {Status}",
                command.PaymentId, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentId}' cannot be refunded — current status is '{payment.Status}'.");
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

        _logger.LogInformation("Refunded payment {PaymentId} for {Amount} {Currency} — reason: {Reason}",
            command.PaymentId, command.Amount, payment.CurrencyCode, command.Reason);

        return PaymentMapper.ToRefundResponse(payment, command.Amount);
    }
}
