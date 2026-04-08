using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class AircraftTypeFunction
{
    private readonly SeatServiceClient _seatServiceClient;
    private readonly ILogger<AircraftTypeFunction> _logger;

    public AircraftTypeFunction(
        SeatServiceClient seatServiceClient,
        ILogger<AircraftTypeFunction> logger)
    {
        _seatServiceClient = seatServiceClient;
        _logger = logger;
    }

    // GET /v1/aircraft-types
    [Function("GetAircraftTypes")]
    [OpenApiOperation(operationId: "GetAircraftTypes", tags: new[] { "Aircraft Types" }, Summary = "List all aircraft types (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetAircraftTypesDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetAircraftTypes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _seatServiceClient.GetAircraftTypesAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve aircraft types");
            return await req.InternalServerErrorAsync();
        }
    }
}
