using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Shared.Common.Health;

namespace ReservationSystem.Microservices.Offer.Functions;

public class HealthCheckFunction(IOfferRepository repository, ILogger<HealthCheckFunction> logger)
{
    [Function("HealthCheck")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        return HealthCheckService.RunAsync(req, logger,
            ($"{repository.GetType().Name}.{nameof(IOfferRepository.GetAllAsync)}",
             ct => repository.GetAllAsync(ct)));
    }
}
