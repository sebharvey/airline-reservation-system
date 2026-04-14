using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ReservationSystem.Shared.Common.Caching;

/// <summary>
/// Registration helpers for the microservice response-cache middleware.
///
/// Call both extension methods from every microservice <c>Program.cs</c>:
///
/// <code>
/// var host = new HostBuilder()
///     .ConfigureFunctionsWorkerDefaults(worker =>
///     {
///         worker.UseMicroserviceCache();   // register middleware (must be first)
///         worker.UseNewtonsoftJson();
///     })
///     .ConfigureServices((context, services) =>
///     {
///         services.AddMicroserviceCache(); // register IMemoryCache + middleware
///         // ... rest of services
///     })
///     .Build();
/// </code>
///
/// Individual endpoints opt in by decorating their function method with
/// <see cref="MicroserviceCacheAttribute"/>. Functions without the attribute
/// are never cached.
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Adds <see cref="MicroserviceCacheMiddleware"/> to the Azure Functions
    /// worker middleware pipeline. Call this before other middleware so cache
    /// hits bypass all downstream processing.
    /// </summary>
    public static IFunctionsWorkerApplicationBuilder UseMicroserviceCache(
        this IFunctionsWorkerApplicationBuilder builder)
        => builder.UseMiddleware<MicroserviceCacheMiddleware>();

    /// <summary>
    /// Registers <see cref="IMemoryCache"/> and <see cref="MicroserviceCacheMiddleware"/>
    /// in the DI container. The middleware is a singleton so the reflection-result
    /// cache and the in-memory response store are shared across all invocations.
    /// </summary>
    public static IServiceCollection AddMicroserviceCache(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<MicroserviceCacheMiddleware>();
        return services;
    }
}
