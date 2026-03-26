using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.VoidPayment;

/// <summary>
/// Handles the <see cref="VoidPaymentCommand"/>.
/// Voids a previously authorised payment, releasing the held funds.
/// Updates the existing PaymentEvent row created at authorisation.
/// </summary>
public sealed class VoidPaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<VoidPaymentHandler> _logger;

    public VoidPaymentHandler(
        IPaymentRepository repository,
        ILogger<VoidPaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Voids the payment identified by the command.
    /// Returns null when the paymentId does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the payment exists but cannot be voided.
    /// </summary>
    public async Task<VoidPaymentResponse?> HandleAsync(
        VoidPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Void requested for unknown payment {PaymentId}", command.PaymentId);
            return null;
        }

        if (payment.Status != PaymentStatus.Authorised)
        {
            _logger.LogWarning("Cannot void payment {PaymentId} — current status is {Status}",
                command.PaymentId, payment.Status);
            throw new InvalidOperationException(
                $"Payment '{command.PaymentId}' cannot be voided — current status is '{payment.Status}'.");
        }

        payment.Void();
        await _repository.UpdateAsync(payment, cancellationToken);

        var paymentEvent = await _repository.GetEventByPaymentIdAsync(payment.PaymentId, cancellationToken);

        if (paymentEvent is not null)
        {
            paymentEvent.Update(PaymentEventType.Voided, payment.AuthorisedAmount ?? 0m,
                $"Payment voided{(string.IsNullOrWhiteSpace(command.Reason) ? "" : $" — reason: {command.Reason}")}");

            await _repository.UpdateEventAsync(paymentEvent, cancellationToken);
        }

        _logger.LogInformation("Voided payment {PaymentId} — reason: {Reason}",
            command.PaymentId, command.Reason ?? "none");

        return PaymentMapper.ToVoidResponse(payment);
    }
}
