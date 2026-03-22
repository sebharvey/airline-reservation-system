using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ReservationSystem.Shared.Common.Health;

/// <summary>
/// Shared Azure Function that handles GET /v1/health for every API.
/// Each API registers its own <see cref="IHealthCheckProvider"/> via
/// <see cref="HealthCheckExtensions.AddHealthCheck"/> in Program.cs — no per-API function class needed.
/// </summary>
public class HealthCheckFunction(IEnumerable<IHealthCheckProvider> providers, ILogger<HealthCheckFunction> logger)
{
    [Function("HealthCheck")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        return HealthCheckService.RunAsync(req, logger,
            providers.Select(p => (p.Name, (Func<CancellationToken, Task>)p.CheckAsync)).ToArray());
    }
}
