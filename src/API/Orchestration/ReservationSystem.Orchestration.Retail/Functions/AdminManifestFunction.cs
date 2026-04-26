using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// Staff-facing endpoint that returns the passenger manifest for a given flight,
/// sourced from delivery.Manifest via the Delivery microservice.
/// Requires a valid staff JWT (enforced by TerminalAuthenticationMiddleware).
/// Function name is prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class AdminManifestFunction
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<AdminManifestFunction> _logger;

    public AdminManifestFunction(
        DeliveryServiceClient deliveryServiceClient,
        ILogger<AdminManifestFunction> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    // GET /v1/admin/manifest?flightNumber=AX001&departureDate=yyyy-MM-dd
    [Function("AdminGetFlightManifest")]
    [OpenApiOperation(operationId: "AdminGetFlightManifest", tags: new[] { "Admin Manifest" },
        Summary = "Return the passenger manifest for a flight from delivery.Manifest (staff)")]
    [OpenApiParameter(name: "flightNumber",   In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Flight number, e.g. AX001")]
    [OpenApiParameter(name: "departureDate",  In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Departure date (yyyy-MM-dd)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminFlightManifestResult), Description = "OK — manifest entries")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,  Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,    Description = "Not Found — no manifest for this flight")]
    public async Task<HttpResponseData> GetFlightManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var flightNumber  = qs["flightNumber"];
        var departureDateRaw = qs["departureDate"];

        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("flightNumber is required.");

        if (string.IsNullOrWhiteSpace(departureDateRaw) ||
            !DateOnly.TryParseExact(departureDateRaw, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("departureDate must be in yyyy-MM-dd format.");

        var result = await _deliveryServiceClient.GetManifestByFlightAsync(
            flightNumber, departureDateRaw, cancellationToken);

        if (result is null)
        {
            _logger.LogWarning("Manifest retrieval failed for {FlightNumber} {DepartureDate}", flightNumber, departureDateRaw);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        return await req.OkJsonAsync(result);
    }
}
