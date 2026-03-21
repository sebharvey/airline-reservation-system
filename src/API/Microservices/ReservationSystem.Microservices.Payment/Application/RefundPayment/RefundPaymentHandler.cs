using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.RefundPayment;

/// <summary>
/// Handles the <see cref="RefundPaymentCommand"/>.
/// Refunds a previously settled payment.
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

    public Task<RefundPaymentResponse> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
