using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Domain.Services;

namespace ReservationSystem.Microservices.Offer.Application.HealthCheck;

/// <summary>
/// Verifies that the Offer repository (and its underlying SQL connection) is reachable.
/// Injected into <see cref="Functions.HealthCheckFunction"/> via DI.
/// </summary>
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        IOfferRepository repository,
        ILogger<HealthCheckService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invoking repository to verify service health");
        await _repository.GetAllAsync(cancellationToken);
        return true;
    }
}
