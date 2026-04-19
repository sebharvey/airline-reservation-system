using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.InitialisePayment;

/// <summary>
/// Handles the <see cref="InitialisePaymentCommand"/>.
/// Creates a Payment record with order details and returns the generated paymentId.
/// </summary>
public sealed class InitialisePaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<InitialisePaymentHandler> _logger;

    public InitialisePaymentHandler(
        IPaymentRepository repository,
        ILogger<InitialisePaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<InitialisePaymentResponse> HandleAsync(
        InitialisePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = Domain.Entities.Payment.Initialise(
            bookingReference: command.BookingReference,
            method: command.Method,
            currencyCode: command.CurrencyCode,
            amount: command.Amount,
            description: command.Description);

        await _repository.CreateAsync(payment, cancellationToken);

        _logger.LogInformation("Initialised payment {PaymentId} for {Amount} {Currency}",
            payment.PaymentId, command.Amount, command.CurrencyCode);

        return PaymentMapper.ToInitialiseResponse(payment);
    }
}
