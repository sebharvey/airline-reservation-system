using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Schedule.Application.CreateScheduleGroup;
using ReservationSystem.Microservices.Schedule.Application.DeleteScheduleGroup;
using ReservationSystem.Microservices.Schedule.Application.GetScheduleGroups;
using ReservationSystem.Microservices.Schedule.Application.UpdateScheduleGroup;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Schedule.Functions;

/// <summary>
/// HTTP-triggered functions for schedule group CRUD.
/// </summary>
public sealed class ScheduleGroupFunction
{
    private readonly GetScheduleGroupsHandler _getScheduleGroupsHandler;
    private readonly CreateScheduleGroupHandler _createScheduleGroupHandler;
    private readonly UpdateScheduleGroupHandler _updateScheduleGroupHandler;
    private readonly DeleteScheduleGroupHandler _deleteScheduleGroupHandler;
    private readonly ILogger<ScheduleGroupFunction> _logger;

    public ScheduleGroupFunction(
        GetScheduleGroupsHandler getScheduleGroupsHandler,
        CreateScheduleGroupHandler createScheduleGroupHandler,
        UpdateScheduleGroupHandler updateScheduleGroupHandler,
        DeleteScheduleGroupHandler deleteScheduleGroupHandler,
        ILogger<ScheduleGroupFunction> logger)
    {
        _getScheduleGroupsHandler = getScheduleGroupsHandler;
        _createScheduleGroupHandler = createScheduleGroupHandler;
        _updateScheduleGroupHandler = updateScheduleGroupHandler;
        _deleteScheduleGroupHandler = deleteScheduleGroupHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/schedule-groups
    // -------------------------------------------------------------------------

    [Function("GetScheduleGroups")]
    [MicroserviceCache("Schedule", 1)]
    [OpenApiOperation(operationId: "GetScheduleGroups", tags: new[] { "ScheduleGroups" }, Summary = "Retrieve all schedule groups")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetScheduleGroupsResponse), Description = "OK — returns all schedule groups")]
    public async Task<HttpResponseData> GetScheduleGroups(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/schedule-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _getScheduleGroupsHandler.HandleAsync(new GetScheduleGroupsQuery(), cancellationToken);
            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve schedule groups");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedule-groups
    // -------------------------------------------------------------------------

    [Function("CreateScheduleGroup")]
    [MicroserviceCacheInvalidate("Schedule")]
    [OpenApiOperation(operationId: "CreateScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Create a new schedule group")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateScheduleGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ScheduleGroupItem), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/schedule-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateScheduleGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("'name' is required.");

        if (string.IsNullOrWhiteSpace(request.SeasonStart) || !DateTime.TryParse(request.SeasonStart, out var seasonStart))
            return await req.BadRequestAsync("'seasonStart' must be a valid date (yyyy-MM-dd).");

        if (string.IsNullOrWhiteSpace(request.SeasonEnd) || !DateTime.TryParse(request.SeasonEnd, out var seasonEnd))
            return await req.BadRequestAsync("'seasonEnd' must be a valid date (yyyy-MM-dd).");

        if (seasonStart > seasonEnd)
            return await req.BadRequestAsync("'seasonStart' must not be after 'seasonEnd'.");

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
            return await req.BadRequestAsync("'createdBy' is required.");

        try
        {
            var result = await _createScheduleGroupHandler.HandleAsync(
                new CreateScheduleGroupCommand(request.Name, seasonStart, seasonEnd, request.IsActive, request.CreatedBy),
                cancellationToken);

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule group");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PUT /v1/schedule-groups/{scheduleGroupId}
    // -------------------------------------------------------------------------

    [Function("UpdateScheduleGroup")]
    [MicroserviceCacheInvalidate("Schedule")]
    [OpenApiOperation(operationId: "UpdateScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Update an existing schedule group")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateScheduleGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ScheduleGroupItem), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
        string scheduleGroupId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scheduleGroupId, out var groupId))
            return await req.BadRequestAsync("Invalid scheduleGroupId.");

        var (request, error) = await req.TryDeserializeBodyAsync<UpdateScheduleGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("'name' is required.");

        if (!DateTime.TryParse(request.SeasonStart, out var seasonStart))
            return await req.BadRequestAsync("'seasonStart' must be a valid date.");

        if (!DateTime.TryParse(request.SeasonEnd, out var seasonEnd))
            return await req.BadRequestAsync("'seasonEnd' must be a valid date.");

        try
        {
            var result = await _updateScheduleGroupHandler.HandleAsync(
                new UpdateScheduleGroupCommand(groupId, request.Name, seasonStart, seasonEnd, request.IsActive),
                cancellationToken);

            if (result is null)
                return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");

            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule group");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/schedule-groups/{scheduleGroupId}
    // -------------------------------------------------------------------------

    [Function("DeleteScheduleGroup")]
    [MicroserviceCacheInvalidate("Schedule")]
    [OpenApiOperation(operationId: "DeleteScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Delete a schedule group and all its schedules")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
        string scheduleGroupId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scheduleGroupId, out var groupId))
            return await req.BadRequestAsync("Invalid scheduleGroupId.");

        try
        {
            var deleted = await _deleteScheduleGroupHandler.HandleAsync(
                new DeleteScheduleGroupCommand(groupId),
                cancellationToken);

            if (!deleted)
                return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule group");
            return await req.InternalServerErrorAsync();
        }
    }
}
