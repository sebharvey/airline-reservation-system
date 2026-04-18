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

public sealed class FareFamilyFunction
{
    private readonly FareFamilyServiceClient _fareFamilyServiceClient;
    private readonly ILogger<FareFamilyFunction> _logger;

    public FareFamilyFunction(
        FareFamilyServiceClient fareFamilyServiceClient,
        ILogger<FareFamilyFunction> logger)
    {
        _fareFamilyServiceClient = fareFamilyServiceClient;
        _logger = logger;
    }

    // GET /v1/admin/fare-families
    [Function("AdminGetFareFamilies")]
    [OpenApiOperation(operationId: "AdminGetFareFamilies", tags: new[] { "Admin Fare Families" }, Summary = "List all fare families (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FareFamilyDto>), Description = "OK")]
    public async Task<HttpResponseData> GetFareFamilies(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/fare-families")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fareFamilyServiceClient.GetFareFamiliesAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fare families");
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/admin/fare-families/{fareFamilyId}
    [Function("AdminGetFareFamily")]
    [OpenApiOperation(operationId: "AdminGetFareFamily", tags: new[] { "Admin Fare Families" }, Summary = "Get a fare family by ID (staff)")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareFamilyDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetFareFamily(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var fareFamily = await _fareFamilyServiceClient.GetFareFamilyAsync(fareFamilyId, cancellationToken);

            if (fareFamily is null)
                return await req.NotFoundAsync($"Fare family '{fareFamilyId}' not found.");

            return await req.OkJsonAsync(fareFamily);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fare family {FareFamilyId}", fareFamilyId);
            return await req.InternalServerErrorAsync();
        }
    }

    // POST /v1/admin/fare-families
    [Function("AdminCreateFareFamily")]
    [OpenApiOperation(operationId: "AdminCreateFareFamily", tags: new[] { "Admin Fare Families" }, Summary = "Create a new fare family (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateFareFamilyRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(FareFamilyDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateFareFamily(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/fare-families")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateFareFamilyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _fareFamilyServiceClient.CreateFareFamilyAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/fare-families/{result.FareFamilyId}", result);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fare family");
            return await req.InternalServerErrorAsync();
        }
    }

    // PUT /v1/admin/fare-families/{fareFamilyId}
    [Function("AdminUpdateFareFamily")]
    [OpenApiOperation(operationId: "AdminUpdateFareFamily", tags: new[] { "Admin Fare Families" }, Summary = "Update a fare family (staff)")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateFareFamilyRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareFamilyDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateFareFamily(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateFareFamilyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _fareFamilyServiceClient.UpdateFareFamilyAsync(fareFamilyId, request!, cancellationToken);
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
            _logger.LogError(ex, "Failed to update fare family {FareFamilyId}", fareFamilyId);
            return await req.InternalServerErrorAsync();
        }
    }

    // DELETE /v1/admin/fare-families/{fareFamilyId}
    [Function("AdminDeleteFareFamily")]
    [OpenApiOperation(operationId: "AdminDeleteFareFamily", tags: new[] { "Admin Fare Families" }, Summary = "Delete a fare family (staff)")]
    [OpenApiParameter(name: "fareFamilyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteFareFamily(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/fare-families/{fareFamilyId:guid}")] HttpRequestData req,
        Guid fareFamilyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _fareFamilyServiceClient.DeleteFareFamilyAsync(fareFamilyId, cancellationToken);

            if (!found)
                return await req.NotFoundAsync($"Fare family '{fareFamilyId}' not found.");

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete fare family {FareFamilyId}", fareFamilyId);
            return await req.InternalServerErrorAsync();
        }
    }
}
