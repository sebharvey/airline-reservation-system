using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.GetPayment;

/// <summary>
/// Handles the <see cref="GetPaymentQuery"/>.
/// Retrieves a payment record by ID from the repository.
/// Returns null when no payment with the given ID exists.
/// </summary>
public sealed class GetPaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<GetPaymentHandler> _logger;

    public GetPaymentHandler(IPaymentRepository repository, ILogger<GetPaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PaymentResponse?> HandleAsync(
        GetPaymentQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(query.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", query.PaymentId);
            return null;
        }

        return PaymentMapper.ToPaymentResponse(payment);
    }
}
