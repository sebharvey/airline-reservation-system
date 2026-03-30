using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.GetFlightStatus;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// Public HTTP-triggered function for real-time flight status.
/// Derives status from the Offer microservice's flight inventory.
/// </summary>
public sealed class FlightStatusFunction
{
    private readonly GetFlightStatusHandler _handler;
    private readonly ILogger<FlightStatusFunction> _logger;

    public FlightStatusFunction(GetFlightStatusHandler handler, ILogger<FlightStatusFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/flights/{flightNumber}/status?date=yyyy-MM-dd
    // -------------------------------------------------------------------------

    [Function("GetFlightStatus")]
    [OpenApiOperation(operationId: "GetFlightStatus", tags: new[] { "Flight Status" }, Summary = "Get real-time flight status for a given flight number and date")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Flight number (e.g. AX001)")]
    [OpenApiParameter(name: "date", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FlightStatusResponse), Description = "OK — returns flight status")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no flight found for the given flight number and date")]
    public async Task<HttpResponseData> GetFlightStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{flightNumber}/status")] HttpRequestData req,
        string flightNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dateParam = qs["date"];

        string departureDate;
        if (string.IsNullOrWhiteSpace(dateParam))
        {
            departureDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        }
        else if (!DateOnly.TryParseExact(dateParam, "yyyy-MM-dd", out var parsed))
        {
            return await req.BadRequestAsync("'date' must be in yyyy-MM-dd format.");
        }
        else
        {
            departureDate = parsed.ToString("yyyy-MM-dd");
        }

        try
        {
            var query = new GetFlightStatusQuery(flightNumber.ToUpperInvariant(), departureDate);
            var result = await _handler.HandleAsync(query, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"No flight found for '{flightNumber}' on {departureDate}.");

            return await req.OkJsonAsync(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in GetFlightStatus");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve flight status for {FlightNumber} on {Date}", flightNumber, departureDate);
            return await req.InternalServerErrorAsync();
        }
    }
}
