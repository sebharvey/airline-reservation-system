using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
using ReservationSystem.Microservices.Schedule.Models.Mappers;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
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
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        CreateScheduleHandler createHandler,
        UpdateScheduleHandler updateHandler,
        ILogger<ScheduleFunction> logger)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules
    // -------------------------------------------------------------------------

    [Function("CreateSchedule")]
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
            var response = ScheduleMapper.ToResponse(schedule);

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

            var response = ScheduleMapper.ToResponse(schedule);
            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule {ScheduleId}", scheduleId);
            return await req.InternalServerErrorAsync();
        }
    }
}
