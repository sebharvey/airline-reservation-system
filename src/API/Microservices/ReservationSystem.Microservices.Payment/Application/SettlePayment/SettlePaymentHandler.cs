using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.SettlePayment;

/// <summary>
/// Handles the <see cref="SettlePaymentCommand"/>.
/// Settles a previously authorised payment.
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

    public Task<SettlePaymentResponse> HandleAsync(
        SettlePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
