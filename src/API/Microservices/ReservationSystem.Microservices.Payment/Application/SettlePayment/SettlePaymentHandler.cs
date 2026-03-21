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
            return null;
        }

        payment.Settle(command.Amount);
        await _repository.UpdateAsync(payment, cancellationToken);

        var paymentEvent = PaymentEvent.Create(
            payment.PaymentId,
            PaymentEventType.Settle,
            command.Amount,
            payment.CurrencyCode,
            $"Payment settled for {command.Amount} {payment.CurrencyCode}");

        await _repository.CreateEventAsync(paymentEvent, cancellationToken);

        _logger.LogInformation("Settled payment {PaymentReference} for {Amount} {Currency}",
            command.PaymentReference, command.Amount, payment.CurrencyCode);

        return PaymentMapper.ToSettleResponse(payment);
    }
}
