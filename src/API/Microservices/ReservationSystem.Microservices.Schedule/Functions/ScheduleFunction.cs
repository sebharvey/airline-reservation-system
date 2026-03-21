using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;
using ReservationSystem.Microservices.Schedule.Application.UpdateSchedule;
using ReservationSystem.Microservices.Schedule.Models.Mappers;
using ReservationSystem.Microservices.Schedule.Models.Requests;

namespace ReservationSystem.Microservices.Schedule.Functions;

/// <summary>
/// HTTP-triggered functions for the Schedule resource.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// All endpoints are boilerplate scaffolds; implementation is pending.
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
