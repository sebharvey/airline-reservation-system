using Microsoft.Extensions.DependencyInjection;

namespace ReservationSystem.Shared.Common.Health;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Registers a named health check delegate that <see cref="HealthCheckFunction"/> will invoke.
    /// Call this once per service/dependency you want verified by GET /v1/health.
    /// </summary>
    public static IServiceCollection AddHealthCheck(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, Func<CancellationToken, Task>> checkFactory)
    {
        return services.AddScoped<IHealthCheckProvider>(sp =>
            new DelegateHealthCheckProvider(name, checkFactory(sp)));
    }
}

internal sealed class DelegateHealthCheckProvider(string name, Func<CancellationToken, Task> check) : IHealthCheckProvider
{
    public string Name => name;
    public Task CheckAsync(CancellationToken ct) => check(ct);
}
