using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.CreateSchedule;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for flight schedule management.
/// Orchestrates calls to the Schedule and Offer microservices to create schedules
/// and generate corresponding inventory.
/// </summary>
public sealed class ScheduleFunction
{
    private readonly CreateScheduleHandler _createScheduleHandler;
    private readonly ImportSsimHandler _importSsimHandler;
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        CreateScheduleHandler createScheduleHandler,
        ImportSsimHandler importSsimHandler,
        ILogger<ScheduleFunction> logger)
    {
        _createScheduleHandler = createScheduleHandler;
        _importSsimHandler = importSsimHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules
    // -------------------------------------------------------------------------

    [Function("CreateSchedule")]
    [OpenApiOperation(operationId: "CreateSchedule", tags: new[] { "Schedules" }, Summary = "Create a flight schedule and generate inventory")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateScheduleRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateScheduleResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateScheduleRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.FlightNumber)
            || string.IsNullOrWhiteSpace(request.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.AircraftType))
        {
            return await req.BadRequestAsync("The fields 'flightNumber', 'origin', 'destination', and 'aircraftType' are required.");
        }

        if (string.IsNullOrWhiteSpace(request.DepartureTime) || string.IsNullOrWhiteSpace(request.ArrivalTime))
            return await req.BadRequestAsync("The fields 'departureTime' and 'arrivalTime' are required.");

        if (string.IsNullOrWhiteSpace(request.ValidFrom) || string.IsNullOrWhiteSpace(request.ValidTo))
            return await req.BadRequestAsync("The fields 'validFrom' and 'validTo' are required.");

        if (request.DaysOfWeek is < 1 or > 127)
            return await req.BadRequestAsync("The 'daysOfWeek' bitmask must be between 1 and 127.");

        if (request.Cabins.Count == 0)
            return await req.BadRequestAsync("At least one cabin definition is required.");

        var command = new CreateScheduleCommand(
            request.FlightNumber,
            request.Origin,
            request.Destination,
            request.DepartureTime,
            request.ArrivalTime,
            request.ArrivalDayOffset,
            request.DaysOfWeek,
            request.AircraftType,
            request.ValidFrom,
            request.ValidTo,
            request.Cabins);

        try
        {
            var result = await _createScheduleHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/schedules/{result.ScheduleId}", result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in CreateSchedule");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule for {FlightNumber}", request.FlightNumber);
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules/ssim
    // -------------------------------------------------------------------------

    [Function("ImportSsim")]
    [OpenApiOperation(operationId: "ImportSsim", tags: new[] { "Schedules" }, Summary = "Import schedules from an IATA SSIM Chapter 7 file")]
    [OpenApiParameter(name: "createdBy", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Identity of the user performing the import (defaults to 'ssim-import')")]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Required = true, Description = "SSIM Chapter 7 plain-text file content")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ImportSsimResponse), Description = "OK — returns count and summary of imported schedules")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> ImportSsim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules/ssim")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var createdBy = qs["createdBy"] ?? "ssim-import";

        string ssimText;
        using (var reader = new System.IO.StreamReader(req.Body))
        {
            ssimText = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(ssimText))
            return await req.BadRequestAsync("Request body must contain SSIM file content.");

        try
        {
            var response = await _importSsimHandler.HandleAsync(
                new ImportSsimCommand(ssimText, createdBy), cancellationToken);

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import SSIM file");
            return await req.InternalServerErrorAsync();
        }
    }
}
