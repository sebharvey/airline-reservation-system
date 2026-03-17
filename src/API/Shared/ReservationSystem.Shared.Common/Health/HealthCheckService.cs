using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Shared.Common.Health;

/// <summary>
/// Shared health check logic for Azure Functions APIs.
/// Accepts one or more named async checks to verify and builds a consistent HTTP response.
/// </summary>
public static class HealthCheckService
{
    /// <summary>
    /// Runs the provided health checks and returns a 200 OK or 503 Service Unavailable response.
    /// </summary>
    /// <param name="req">The incoming HTTP request.</param>
    /// <param name="logger">Logger for the calling function.</param>
    /// <param name="checks">One or more named checks to execute. Name is used in the response 'service' field.</param>
    public static async Task<HttpResponseData> RunAsync(
        HttpRequestData req,
        ILogger logger,
        params (string Name, Func<CancellationToken, Task> Check)[] checks)
    {
        logger.LogInformation("Health check requested");

        var ct = req.FunctionContext.CancellationToken;
        var serviceNames = string.Join(", ", checks.Select(c => c.Name));

        foreach (var (name, check) in checks)
        {
            try
            {
                await check(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed");

                var unhealthy = new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    service = name,
                    version = "1.0.0",
                    error = ex.Message,
                    checks = new { serviceCheck = "error", fileSystem = "error" }
                };

                var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(unhealthy, SharedJsonOptions.CamelCase));
                return errorResponse;
            }
        }

        var healthy = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = serviceNames,
            version = "1.0.0",
            checks = new { serviceCheck = "ok", fileSystem = "ok" }
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        await response.WriteStringAsync(JsonSerializer.Serialize(healthy, SharedJsonOptions.CamelCase));
        return response;
    }
}
