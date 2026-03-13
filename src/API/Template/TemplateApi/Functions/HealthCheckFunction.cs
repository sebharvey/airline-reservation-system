using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Template.TemplateApi.Functions;

public class HealthCheckFunction
{
    private readonly ILogger<HealthCheckFunction> _logger;

    public HealthCheckFunction(ILogger<HealthCheckFunction> logger)
    {
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check requested");

        try
        {
            // Check if we can access directory
            // TODO implement a call to a service inside this function to check it works
            
            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "TemplateService",  // This would be the name of the API, like OfferService or OrderService
                version = "1.0.0",
                checks = new
                {
                    serviceCheck = "ok",
                    fileSystem = "ok"
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
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
                service = "TemplateService",  // This would be the name of the API, like OfferService or OrderService
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
