using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Services;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Functions;

public class HealthCheckFunction
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthCheckFunction> _logger;

    public HealthCheckFunction(
        IHealthCheckService healthCheckService,
        ILogger<HealthCheckFunction> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check requested");

        try
        {
            var serviceHealthy = await _healthCheckService.IsHealthyAsync(req.FunctionContext.CancellationToken);

            var healthStatus = new
            {
                status = serviceHealthy ? "healthy" : "unhealthy",
                timestamp = DateTime.UtcNow,
                service = "OfferService",
                version = "1.0.0",
                checks = new
                {
                    serviceCheck = serviceHealthy ? "ok" : "error",
                    fileSystem = "ok"
                }
            };

            var statusCode = serviceHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus, SharedJsonOptions.CamelCase));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            var healthStatus = new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                service = "OfferService",
                version = "1.0.0",
                error = ex.Message,
                checks = new
                {
                    serviceCheck = "error",
                    fileSystem = "error"
                }
            };

            var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus, SharedJsonOptions.CamelCase));

            return response;
        }
    }
}
