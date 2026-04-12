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

public sealed class AdminBagPolicyFunction
{
    private readonly BagServiceClient _bagServiceClient;
    private readonly ILogger<AdminBagPolicyFunction> _logger;

    public AdminBagPolicyFunction(BagServiceClient bagServiceClient, ILogger<AdminBagPolicyFunction> logger)
    {
        _bagServiceClient = bagServiceClient;
        _logger = logger;
    }

    [Function("AdminGetAllBagPolicies")]
    [OpenApiOperation(operationId: "AdminGetAllBagPolicies", tags: new[] { "Admin Bag Policies" }, Summary = "List all bag policies (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<BagPolicyDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bagServiceClient.GetAllBagPoliciesAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list bag policies");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminGetBagPolicy")]
    [OpenApiOperation(operationId: "AdminGetBagPolicy", tags: new[] { "Admin Bag Policies" }, Summary = "Get a bag policy by ID (staff)")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPolicyDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _bagServiceClient.GetBagPolicyAsync(policyId, cancellationToken);
            if (policy is null)
                return await req.NotFoundAsync($"Bag policy '{policyId}' not found.");
            return await req.OkJsonAsync(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bag policy {PolicyId}", policyId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminCreateBagPolicy")]
    [OpenApiOperation(operationId: "AdminCreateBagPolicy", tags: new[] { "Admin Bag Policies" }, Summary = "Create a new bag policy (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateBagPolicyRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BagPolicyDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — cabin code already has a policy")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateBagPolicyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _bagServiceClient.CreateBagPolicyAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/bag-policies/{result.PolicyId}", result);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bag policy");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateBagPolicy")]
    [OpenApiOperation(operationId: "AdminUpdateBagPolicy", tags: new[] { "Admin Bag Policies" }, Summary = "Update a bag policy (staff)")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateBagPolicyRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPolicyDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateBagPolicyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _bagServiceClient.UpdateBagPolicyAsync(policyId, request!, cancellationToken);
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
            _logger.LogError(ex, "Failed to update bag policy {PolicyId}", policyId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteBagPolicy")]
    [OpenApiOperation(operationId: "AdminDeleteBagPolicy", tags: new[] { "Admin Bag Policies" }, Summary = "Delete a bag policy (staff)")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _bagServiceClient.DeleteBagPolicyAsync(policyId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Bag policy '{policyId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bag policy {PolicyId}", policyId);
            return await req.InternalServerErrorAsync();
        }
    }
}
