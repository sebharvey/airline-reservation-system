using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.Ssim;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
using ReservationSystem.Microservices.Schedule.Models.Mappers;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Schedule.Functions;

/// <summary>
/// HTTP-triggered functions for the Schedule resource.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// </summary>
public sealed class ScheduleFunction
{
    private readonly CreateScheduleHandler _createHandler;
    private readonly UpdateScheduleHandler _updateHandler;
    private readonly ImportSsimHandler _importSsimHandler;
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        CreateScheduleHandler createHandler,
        UpdateScheduleHandler updateHandler,
        ImportSsimHandler importSsimHandler,
        ILogger<ScheduleFunction> logger)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _importSsimHandler = importSsimHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules
    // -------------------------------------------------------------------------

    [Function("CreateSchedule")]
    [OpenApiOperation(operationId: "CreateSchedule", tags: new[] { "Schedules" }, Summary = "Create a flight schedule")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateScheduleRequest), Required = true, Description = "Schedule details: flightNumber, origin, destination, departureTime, arrivalTime, aircraftType, daysOfWeek, createdBy")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateScheduleResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateScheduleRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateScheduleRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateSchedule request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.FlightNumber))
            return await req.BadRequestAsync("The 'flightNumber' field is required.");

        if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
            return await req.BadRequestAsync("The 'origin' and 'destination' fields are required.");

        if (string.IsNullOrWhiteSpace(request.DepartureTime) || string.IsNullOrWhiteSpace(request.ArrivalTime))
            return await req.BadRequestAsync("The 'departureTime' and 'arrivalTime' fields are required.");

        if (string.IsNullOrWhiteSpace(request.AircraftType))
            return await req.BadRequestAsync("The 'aircraftType' field is required.");

        if (request.DaysOfWeek == 0)
            return await req.BadRequestAsync("The 'daysOfWeek' bitmask must be non-zero.");

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
            return await req.BadRequestAsync("The 'createdBy' field is required.");

        try
        {
            var command = ScheduleMapper.ToCommand(request);
            var schedule = await _createHandler.HandleAsync(command, cancellationToken);
            var response = ScheduleMapper.ToCreateResponse(schedule);

            return await req.CreatedAsync($"/v1/schedules/{schedule.ScheduleId}", response);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid format in CreateSchedule request");
            return await req.BadRequestAsync("Invalid time or date format. Use HH:mm for times and yyyy-MM-dd for dates.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/schedules/{scheduleId:guid}
    // -------------------------------------------------------------------------

    [Function("UpdateSchedule")]
    [OpenApiOperation(operationId: "UpdateSchedule", tags: new[] { "Schedules" }, Summary = "Update a flight schedule")]
    [OpenApiParameter(name: "scheduleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Schedule ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateScheduleRequest), Required = true, Description = "Schedule update details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateScheduleResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/schedules/{scheduleId:guid}")] HttpRequestData req,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        UpdateScheduleRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateScheduleRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateSchedule request for {ScheduleId}", scheduleId);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = ScheduleMapper.ToCommand(scheduleId, request);

        try
        {
            var schedule = await _updateHandler.HandleAsync(command, cancellationToken);

            if (schedule is null)
                return await req.NotFoundAsync($"Schedule '{scheduleId}' not found.");

            var response = ScheduleMapper.ToUpdateResponse(schedule);
            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule {ScheduleId}", scheduleId);
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
            var schedules = await _importSsimHandler.HandleAsync(
                new ImportSsimCommand(ssimText, createdBy), cancellationToken);

            var response = ScheduleMapper.ToImportResponse(schedules);
            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import SSIM file");
            return await req.InternalServerErrorAsync();
        }
    }
}
