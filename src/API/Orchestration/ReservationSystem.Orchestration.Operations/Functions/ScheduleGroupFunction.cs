using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for schedule group CRUD via the Schedule MS.
/// </summary>
public sealed class ScheduleGroupFunction
{
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly ILogger<ScheduleGroupFunction> _logger;

    public ScheduleGroupFunction(
        ScheduleServiceClient scheduleServiceClient,
        ILogger<ScheduleGroupFunction> logger)
    {
        _scheduleServiceClient = scheduleServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/schedule-groups
    // -------------------------------------------------------------------------

    [Function("AdminGetScheduleGroups")]
    [OpenApiOperation(operationId: "GetScheduleGroups", tags: new[] { "ScheduleGroups" }, Summary = "Retrieve all schedule groups")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetScheduleGroupsResponse), Description = "OK — returns all schedule groups")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetScheduleGroups(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/schedule-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _scheduleServiceClient.GetScheduleGroupsAsync(cancellationToken);

            var response = new GetScheduleGroupsResponse
            {
                Count = result.Count,
                Groups = result.Groups.Select(g => new ScheduleGroupSummary
                {
                    ScheduleGroupId = g.ScheduleGroupId,
                    Name = g.Name,
                    SeasonStart = g.SeasonStart,
                    SeasonEnd = g.SeasonEnd,
                    IsActive = g.IsActive,
                    ScheduleCount = g.ScheduleCount,
                    CreatedBy = g.CreatedBy,
                    CreatedAt = g.CreatedAt
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
    // POST /v1/admin/schedule-groups
    // -------------------------------------------------------------------------

    [Function("AdminCreateScheduleGroup")]
    [OpenApiOperation(operationId: "CreateScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Create a new schedule group")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateScheduleGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ScheduleGroupSummary), Description = "OK — returns the created schedule group")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/schedule-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateScheduleGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("'name' is required.");

        if (string.IsNullOrWhiteSpace(request.SeasonStart) || string.IsNullOrWhiteSpace(request.SeasonEnd))
            return await req.BadRequestAsync("'seasonStart' and 'seasonEnd' are required.");

        try
        {
            var result = await _scheduleServiceClient.CreateScheduleGroupAsync(
                new
                {
                    name = request.Name,
                    seasonStart = request.SeasonStart,
                    seasonEnd = request.SeasonEnd,
                    isActive = request.IsActive,
                    createdBy = request.CreatedBy
                }, cancellationToken);

            var response = new ScheduleGroupSummary
            {
                ScheduleGroupId = result.ScheduleGroupId,
                Name = result.Name,
                SeasonStart = result.SeasonStart,
                SeasonEnd = result.SeasonEnd,
                IsActive = result.IsActive,
                ScheduleCount = result.ScheduleCount,
                CreatedBy = result.CreatedBy,
                CreatedAt = result.CreatedAt
            };

            return await req.OkJsonAsync(response);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule group");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/schedule-groups/{scheduleGroupId}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateScheduleGroup")]
    [OpenApiOperation(operationId: "UpdateScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Update an existing schedule group")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateScheduleGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ScheduleGroupSummary), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
        string scheduleGroupId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scheduleGroupId, out var groupId))
            return await req.BadRequestAsync("Invalid scheduleGroupId.");

        var (request, error) = await req.TryDeserializeBodyAsync<UpdateScheduleGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _scheduleServiceClient.UpdateScheduleGroupAsync(groupId,
                new
                {
                    name = request.Name,
                    seasonStart = request.SeasonStart,
                    seasonEnd = request.SeasonEnd,
                    isActive = request.IsActive
                }, cancellationToken);

            var response = new ScheduleGroupSummary
            {
                ScheduleGroupId = result.ScheduleGroupId,
                Name = result.Name,
                SeasonStart = result.SeasonStart,
                SeasonEnd = result.SeasonEnd,
                IsActive = result.IsActive,
                ScheduleCount = result.ScheduleCount,
                CreatedBy = result.CreatedBy,
                CreatedAt = result.CreatedAt
            };

            return await req.OkJsonAsync(response);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule group");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/schedule-groups/{scheduleGroupId}
    // -------------------------------------------------------------------------

    [Function("AdminDeleteScheduleGroup")]
    [OpenApiOperation(operationId: "DeleteScheduleGroup", tags: new[] { "ScheduleGroups" }, Summary = "Delete a schedule group and all its schedules")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteScheduleGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/schedule-groups/{scheduleGroupId}")] HttpRequestData req,
        string scheduleGroupId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scheduleGroupId, out var groupId))
            return await req.BadRequestAsync("Invalid scheduleGroupId.");

        try
        {
            await _scheduleServiceClient.DeleteScheduleGroupAsync(groupId, cancellationToken);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"Schedule group '{scheduleGroupId}' not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule group");
            return await req.InternalServerErrorAsync();
        }
    }
}
