using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// Staff-facing aircraft type list endpoint. Routes under /admin so that
/// TerminalAuthenticationMiddleware is applied automatically.
/// </summary>
public sealed class AdminAircraftTypesFunction
{
    private readonly SeatServiceClient _seatServiceClient;

    public AdminAircraftTypesFunction(SeatServiceClient seatServiceClient)
    {
        _seatServiceClient = seatServiceClient;
    }

    // GET /v1/admin/aircraft-types
    [Function("AdminGetAllAircraftTypes")]
    [OpenApiOperation(operationId: "AdminGetAllAircraftTypes", tags: new[] { "Admin Aircraft Types" }, Summary = "List all aircraft types (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<AircraftTypeDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var types = await _seatServiceClient.GetAllAircraftTypesAsync(cancellationToken);
        return await req.OkJsonAsync(types);
    }
}
