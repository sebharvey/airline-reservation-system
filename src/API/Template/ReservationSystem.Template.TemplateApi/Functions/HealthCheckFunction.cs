using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Functions;

public class HealthCheckFunction(ITemplateItemRepository repository, ILogger<HealthCheckFunction> logger)
{
    [Function("HealthCheck")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        return HealthCheckService.RunAsync(req, logger,
            ($"{repository.GetType().Name}.{nameof(ITemplateItemRepository.GetAllAsync)}",
             ct => repository.GetAllAsync(ct)));
    }
}
