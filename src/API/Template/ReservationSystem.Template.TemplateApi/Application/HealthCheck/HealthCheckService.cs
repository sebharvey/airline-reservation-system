using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
using ReservationSystem.Template.TemplateApi.Domain.Services;

namespace ReservationSystem.Template.TemplateApi.Application.HealthCheck;

/// <summary>
/// Verifies that the TemplateItem repository (and its underlying SQL connection) is reachable.
/// Injected into <see cref="Functions.HealthCheckFunction"/> via DI.
/// </summary>
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly ITemplateItemRepository _repository;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        ITemplateItemRepository repository,
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
