using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;
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
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly ILogger<ScheduleGroupFunction> _logger;

    public ScheduleGroupFunction(
        IScheduleGroupRepository groupRepository,
        IFlightScheduleRepository scheduleRepository,
        ILogger<ScheduleGroupFunction> logger)
    {
        _groupRepository = groupRepository;
        _scheduleRepository = scheduleRepository;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/schedule-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var groups = await _groupRepository.GetAllAsync(cancellationToken);
            var allSchedules = await _scheduleRepository.GetAllAsync(cancellationToken);
            var countByGroup = allSchedules.GroupBy(s => s.ScheduleGroupId)
                .ToDictionary(g => g.Key, g => g.Count());

            var response = new GetScheduleGroupsResponse
            {
                Count = groups.Count,
                Groups = groups.Select(g => new ScheduleGroupItem
                {
                    ScheduleGroupId = g.ScheduleGroupId,
                    Name = g.Name,
                    SeasonStart = g.SeasonStart.ToString("yyyy-MM-dd"),
                    SeasonEnd = g.SeasonEnd.ToString("yyyy-MM-dd"),
                    IsActive = g.IsActive,
                    ScheduleCount = countByGroup.GetValueOrDefault(g.ScheduleGroupId, 0),
                    CreatedBy = g.CreatedBy,
                    CreatedAt = g.CreatedAt.ToString("o")
                }).ToList().AsReadOnly()
            };

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedule-groups")] HttpRequestData req,
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
            var group = ScheduleGroup.Create(request.Name, seasonStart, seasonEnd, request.IsActive, request.CreatedBy);
            await _groupRepository.AddAsync(group, cancellationToken);

            var response = new ScheduleGroupItem
            {
                ScheduleGroupId = group.ScheduleGroupId,
                Name = group.Name,
                SeasonStart = group.SeasonStart.ToString("yyyy-MM-dd"),
                SeasonEnd = group.SeasonEnd.ToString("yyyy-MM-dd"),
                IsActive = group.IsActive,
                ScheduleCount = 0,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt.ToString("o")
            };

            return await req.OkJsonAsync(response);
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
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
            var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
            if (group is null)
                return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");

            group.Update(request.Name, seasonStart, seasonEnd, request.IsActive);
            await _groupRepository.UpdateAsync(group, cancellationToken);

            var schedules = await _scheduleRepository.GetByGroupAsync(groupId, cancellationToken);

            var response = new ScheduleGroupItem
            {
                ScheduleGroupId = group.ScheduleGroupId,
                Name = group.Name,
                SeasonStart = group.SeasonStart.ToString("yyyy-MM-dd"),
                SeasonEnd = group.SeasonEnd.ToString("yyyy-MM-dd"),
                IsActive = group.IsActive,
                ScheduleCount = schedules.Count,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt.ToString("o")
            };

            return await req.OkJsonAsync(response);
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
        string scheduleGroupId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scheduleGroupId, out var groupId))
            return await req.BadRequestAsync("Invalid scheduleGroupId.");

        try
        {
            var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
            if (group is null)
                return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");

            await _groupRepository.DeleteAsync(groupId, cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule group");
            return await req.InternalServerErrorAsync();
        }
    }
}
