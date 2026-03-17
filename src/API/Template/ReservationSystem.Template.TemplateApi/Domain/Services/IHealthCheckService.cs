namespace ReservationSystem.Template.TemplateApi.Domain.Services;

/// <summary>
/// Port (interface) for verifying that the service and its dependencies are operational.
/// Defined in Domain so the Functions layer can depend on it without taking a dependency
/// on Application or Infrastructure. The implementation lives in
/// Application/HealthCheck and is registered via DI at startup.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs a lightweight check to confirm the service is healthy.
    /// Returns <c>true</c> when all dependencies respond successfully.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
