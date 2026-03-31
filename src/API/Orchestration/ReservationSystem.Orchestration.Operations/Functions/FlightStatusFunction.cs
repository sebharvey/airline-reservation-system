using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.GetFlightStatus;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
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
    private readonly OfferServiceClient _offerServiceClient;
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly ILogger<FlightStatusFunction> _logger;

    public FlightStatusFunction(
        GetFlightStatusHandler handler,
        OfferServiceClient offerServiceClient,
        ScheduleServiceClient scheduleServiceClient,
        ILogger<FlightStatusFunction> logger)
    {
        _handler = handler;
        _offerServiceClient = offerServiceClient;
        _scheduleServiceClient = scheduleServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/flights?date=yyyy-MM-dd
    // -------------------------------------------------------------------------

    [Function("GetFlights")]
    [OpenApiOperation(operationId: "GetFlights", tags: new[] { "Flight Status" }, Summary = "List available flights for a given date")]
    [OpenApiParameter(name: "date", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FlightSummaryResponse>), Description = "OK — returns list of flights")]
    public async Task<HttpResponseData> GetFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
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
            var inventories = await _offerServiceClient.GetFlightsByDateAsync(departureDate, cancellationToken);

            var flights = inventories
                .OrderBy(i => i.DepartureTime)
                .ThenBy(i => i.FlightNumber)
                .Select(i => new FlightSummaryResponse
                {
                    FlightNumber  = i.FlightNumber,
                    Origin        = i.Origin,
                    Destination   = i.Destination,
                    DepartureTime = i.DepartureTime,
                    AircraftType  = i.AircraftType
                })
                .ToList();

            return await req.OkJsonAsync(flights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve flights for {Date}", departureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/flights/{flightNumber}/status
    // -------------------------------------------------------------------------

    [Function("GetFlightStatus")]
    [OpenApiOperation(operationId: "GetFlightStatus", tags: new[] { "Flight Status" }, Summary = "Get real-time flight status for a given flight number (today's date)")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Flight number (e.g. AX001)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FlightStatusResponse), Description = "OK — returns flight status")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no flight found for the given flight number today")]
    public async Task<HttpResponseData> GetFlightStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{flightNumber}/status")] HttpRequestData req,
        string flightNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        var departureDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            var query = new GetFlightStatusQuery(flightNumber.ToUpperInvariant(), departureDate);
            var result = await _handler.HandleAsync(query, cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"No flight found for '{flightNumber}' today.");

            return await req.OkJsonAsync(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in GetFlightStatus");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve flight status for {FlightNumber}", flightNumber);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/flight-numbers
    // -------------------------------------------------------------------------

    [Function("GetFlightNumbers")]
    [OpenApiOperation(operationId: "GetFlightNumbers", tags: new[] { "Flight Status" }, Summary = "List flight numbers and route details from active schedules for the current period")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FlightNumberResponse>), Description = "OK — returns distinct flight numbers with route and time details")]
    public async Task<HttpResponseData> GetFlightNumbers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flight-numbers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var schedules = await _scheduleServiceClient.GetSchedulesAsync(cancellationToken: cancellationToken);

            var flights = schedules.Schedules
                .Where(s =>
                    DateOnly.TryParse(s.ValidFrom, out var from) &&
                    DateOnly.TryParse(s.ValidTo, out var to) &&
                    today >= from && today <= to)
                .GroupBy(s => s.FlightNumber)
                .Select(g =>
                {
                    var first = g.First();
                    return new FlightNumberResponse
                    {
                        FlightNumber  = first.FlightNumber,
                        Origin        = first.Origin,
                        Destination   = first.Destination,
                        DepartureTime = first.DepartureTime,
                        ArrivalTime   = first.ArrivalTime
                    };
                })
                .OrderBy(f => f.FlightNumber)
                .ToList()
                .AsReadOnly();

            return await req.OkJsonAsync(flights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve flight numbers");
            return await req.InternalServerErrorAsync();
        }
    }
}
