using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class FareRulesFunction
{
    private readonly FareRuleServiceClient _fareRuleServiceClient;
    private readonly ILogger<FareRulesFunction> _logger;

    public FareRulesFunction(
        FareRuleServiceClient fareRuleServiceClient,
        ILogger<FareRulesFunction> logger)
    {
        _fareRuleServiceClient = fareRuleServiceClient;
        _logger = logger;
    }

    // POST /v1/admin/fare-rules/search
    [Function("AdminSearchFareRules")]
    [OpenApiOperation(operationId: "AdminSearchFareRules", tags: new[] { "Admin Fare Rules" }, Summary = "Search fare rules (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminSearchFareRulesRequest), Required = false)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FareRuleDto>), Description = "OK")]
    public async Task<HttpResponseData> SearchFareRules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/fare-rules/search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (body, _) = await req.TryDeserializeBodyAsync<AdminSearchFareRulesRequest>(_logger, cancellationToken);

        try
        {
            var result = await _fareRuleServiceClient.SearchFareRulesAsync(body?.Query, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search fare rules");
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/admin/fare-rules/{fareRuleId}
    [Function("AdminGetFareRule")]
    [OpenApiOperation(operationId: "AdminGetFareRule", tags: new[] { "Admin Fare Rules" }, Summary = "Get fare rule details (staff)")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareRuleDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetFareRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var fareRule = await _fareRuleServiceClient.GetFareRuleAsync(fareRuleId, cancellationToken);

            if (fareRule is null)
                return await req.NotFoundAsync($"Fare rule '{fareRuleId}' not found.");

            return await req.OkJsonAsync(fareRule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fare rule {FareRuleId}", fareRuleId);
            return await req.InternalServerErrorAsync();
        }
    }

    // POST /v1/admin/fare-rules
    [Function("AdminCreateFareRule")]
    [OpenApiOperation(operationId: "AdminCreateFareRule", tags: new[] { "Admin Fare Rules" }, Summary = "Create a new fare rule (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateFareRuleRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(FareRuleDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateFareRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/fare-rules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateFareRuleRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _fareRuleServiceClient.CreateFareRuleAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/fare-rules/{result.FareRuleId}", result);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fare rule");
            return await req.InternalServerErrorAsync();
        }
    }

    // PUT /v1/admin/fare-rules/{fareRuleId}
    [Function("AdminUpdateFareRule")]
    [OpenApiOperation(operationId: "AdminUpdateFareRule", tags: new[] { "Admin Fare Rules" }, Summary = "Update an existing fare rule (staff)")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateFareRuleRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareRuleDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateFareRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateFareRuleRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _fareRuleServiceClient.UpdateFareRuleAsync(fareRuleId, request!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex)
        {
            return await req.NotFoundAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update fare rule {FareRuleId}", fareRuleId);
            return await req.InternalServerErrorAsync();
        }
    }

    // DELETE /v1/admin/fare-rules/{fareRuleId}
    [Function("AdminDeleteFareRule")]
    [OpenApiOperation(operationId: "AdminDeleteFareRule", tags: new[] { "Admin Fare Rules" }, Summary = "Delete a fare rule (staff)")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteFareRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _fareRuleServiceClient.DeleteFareRuleAsync(fareRuleId, cancellationToken);

            if (!found)
                return await req.NotFoundAsync($"Fare rule '{fareRuleId}' not found.");

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete fare rule {FareRuleId}", fareRuleId);
            return await req.InternalServerErrorAsync();
        }
    }
}
