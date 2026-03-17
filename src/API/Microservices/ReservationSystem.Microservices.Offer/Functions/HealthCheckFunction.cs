using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Functions;

public class HealthCheckFunction
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<HealthCheckFunction> _logger;

    public HealthCheckFunction(
        IOfferRepository repository,
        ILogger<HealthCheckFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check requested");

        var service = $"{_repository.GetType().Name}.{nameof(IOfferRepository.GetAllAsync)}";

        try
        {
            await _repository.GetAllAsync(req.FunctionContext.CancellationToken);

            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service,
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
                service,
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
