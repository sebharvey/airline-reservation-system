using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Operations.Application.CreateSchedule;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for flight schedule management.
/// Orchestrates calls to the Schedule and Offer microservices to create schedules
/// and generate corresponding inventory.
/// </summary>
public sealed class ScheduleFunction
{
    private readonly CreateScheduleHandler _createScheduleHandler;
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        CreateScheduleHandler createScheduleHandler,
        ILogger<ScheduleFunction> logger)
    {
        _createScheduleHandler = createScheduleHandler;
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

        if (request is null
            || string.IsNullOrWhiteSpace(request.FlightNumber)
            || string.IsNullOrWhiteSpace(request.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.AircraftType))
        {
            return await req.BadRequestAsync("The fields 'flightNumber', 'origin', 'destination', and 'aircraftType' are required.");
        }

        var command = new CreateScheduleCommand(
            request.FlightNumber,
            request.Origin,
            request.Destination,
            request.DepartureTime,
            request.ArrivalTime,
            request.AircraftType,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.OperatingDays);

        var result = await _createScheduleHandler.HandleAsync(command, cancellationToken);
        return await req.CreatedAsync($"/v1/schedules/{result.ScheduleId}", result);
    }
}
