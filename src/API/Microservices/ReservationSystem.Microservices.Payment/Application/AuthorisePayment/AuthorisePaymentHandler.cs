using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.AuthorisePayment;

/// <summary>
/// Handles the <see cref="AuthorisePaymentCommand"/>.
/// Authorises a payment and persists it via the domain factory.
/// </summary>
public sealed class AuthorisePaymentHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<AuthorisePaymentHandler> _logger;

    public AuthorisePaymentHandler(
        IPaymentRepository repository,
        ILogger<AuthorisePaymentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<AuthorisePaymentResponse> HandleAsync(
        AuthorisePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
