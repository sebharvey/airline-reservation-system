using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.SettlePayment;

/// <summary>
/// Handles the <see cref="SettlePaymentCommand"/>.
/// Settles a previously authorised payment by capturing the funds.
/// Updates the existing PaymentEvent row created at authorisation.
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
    /// Returns null when the paymentId does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be settled.
    /// </summary>
    public async Task<SettlePaymentResponse?> HandleAsync(
        SettlePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Settlement requested for unknown payment {PaymentId}", command.PaymentId);
            return null;
        }

        if (payment.Status != PaymentStatus.Authorised)
        {
            _logger.LogWarning("Cannot settle payment {PaymentId} — current status is {Status}",
                command.PaymentId, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentId}' cannot be settled — current status is '{payment.Status}'.");
        }

        // Total settled after this call must not exceed total authorised
        var totalSettledAfter = (payment.SettledAmount ?? 0m) + command.Amount;
        if (totalSettledAfter > payment.AuthorisedAmount)
        {
            throw new ArgumentException(
                $"Total settled amount ({totalSettledAfter}) would exceed total authorised amount ({payment.AuthorisedAmount}).");
        }

        // TODO: Call payment gateway to capture / settle the authorised funds.
        // Use the gateway authorisation reference obtained during the authorise step.
        // On failure, leave the payment in Authorised status and return an error.

        payment.Settle(command.Amount);
        await _repository.UpdateAsync(payment, cancellationToken);

        // Derive event type from post-settle status: fully settled → Settled, otherwise PartialSettlement
        var eventType = payment.Status == PaymentStatus.Settled
            ? PaymentEventType.Settled
            : PaymentEventType.PartialSettlement;

        var settleEvent = PaymentEvent.Create(
            payment.PaymentId,
            eventType,
            command.Amount,
            payment.CurrencyCode,
            $"Payment settled for {command.Amount} {payment.CurrencyCode}");

        await _repository.CreateEventAsync(settleEvent, cancellationToken);

        _logger.LogInformation("Settled payment {PaymentId} for {Amount} {Currency}",
            command.PaymentId, command.Amount, payment.CurrencyCode);

        return PaymentMapper.ToSettleResponse(payment);
    }
}
