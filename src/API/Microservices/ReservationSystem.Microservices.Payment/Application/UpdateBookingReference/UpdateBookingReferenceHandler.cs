using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;

namespace ReservationSystem.Microservices.Payment.Application.UpdateBookingReference;

/// <summary>
/// Handles the <see cref="UpdateBookingReferenceCommand"/>.
/// Links a confirmed booking reference to the payment record once the order is confirmed.
/// Returns false when the paymentId does not exist.
/// </summary>
public sealed class UpdateBookingReferenceHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<UpdateBookingReferenceHandler> _logger;

    public UpdateBookingReferenceHandler(
        IPaymentRepository repository,
        ILogger<UpdateBookingReferenceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        UpdateBookingReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("UpdateBookingReference requested for unknown payment {PaymentId}", command.PaymentId);
            return false;
        }

        payment.SetBookingReference(command.BookingReference);
        await _repository.UpdateAsync(payment, cancellationToken);

        _logger.LogInformation("Linked payment {PaymentId} to booking {BookingReference}",
            command.PaymentId, command.BookingReference);

        return true;
    }
}
