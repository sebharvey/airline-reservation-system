using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.DeleteBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPolicies;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPolicy;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Responses;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Ancillary.Functions.Bag;

public sealed class BagPolicyFunction
{
    private readonly GetBagPolicyHandler _getHandler;
    private readonly GetAllBagPoliciesHandler _getAllHandler;
    private readonly CreateBagPolicyHandler _createHandler;
    private readonly UpdateBagPolicyHandler _updateHandler;
    private readonly DeleteBagPolicyHandler _deleteHandler;
    private readonly ILogger<BagPolicyFunction> _logger;

    public BagPolicyFunction(
        GetBagPolicyHandler getHandler,
        GetAllBagPoliciesHandler getAllHandler,
        CreateBagPolicyHandler createHandler,
        UpdateBagPolicyHandler updateHandler,
        DeleteBagPolicyHandler deleteHandler,
        ILogger<BagPolicyFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    [Function("GetAllBagPolicies")]
    [MicroserviceCache(24)]
    [OpenApiOperation(operationId: "GetAllBagPolicies", tags: new[] { "BagPolicies" }, Summary = "List all bag policies")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPoliciesListResponse), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var policies = await _getAllHandler.HandleAsync(new GetAllBagPoliciesQuery(), cancellationToken);
        var body = new { policies = BagMapper.ToResponse(policies) };
        return await req.OkJsonAsync(body);
    }

    [Function("GetBagPolicy")]
    [OpenApiOperation(operationId: "GetBagPolicy", tags: new[] { "BagPolicies" }, Summary = "Get a bag policy by ID")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag policy ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPolicyResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var policy = await _getHandler.HandleAsync(new GetBagPolicyQuery(policyId), cancellationToken);
        if (policy is null)
            return await req.NotFoundAsync($"No policy found for ID '{policyId}'.");
        return await req.OkJsonAsync(BagMapper.ToResponse(policy));
    }

    [Function("CreateBagPolicy")]
    [OpenApiOperation(operationId: "CreateBagPolicy", tags: new[] { "BagPolicies" }, Summary = "Create a new bag policy")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBagPolicyRequest), Required = true, Description = "The bag policy to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BagPolicyResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateBagPolicyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.CabinCode))
            return await req.BadRequestAsync("The 'cabinCode' field is required.");

        if (request.CabinCode is not ("F" or "J" or "W" or "Y"))
            return await req.BadRequestAsync("cabinCode must be one of: F, J, W, Y.");

        if (request.FreeBagsIncluded < 0)
            return await req.BadRequestAsync("freeBagsIncluded must be >= 0.");

        if (request.MaxWeightKgPerBag <= 0)
            return await req.BadRequestAsync("maxWeightKgPerBag must be > 0.");

        try
        {
            var command = BagMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            var response = BagMapper.ToResponse(created);
            return await req.CreatedAsync($"/v1/bag-policies/{created.PolicyId}", response);
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("UQ_BagPolicy_Cabin") == true
                                   || ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
        {
            return await req.ConflictAsync($"A policy already exists for cabin code '{request.CabinCode}'.");
        }
    }

    [Function("UpdateBagPolicy")]
    [OpenApiOperation(operationId: "UpdateBagPolicy", tags: new[] { "BagPolicies" }, Summary = "Update a bag policy")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag policy ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateBagPolicyRequest), Required = true, Description = "The update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPolicyResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateBagPolicyRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.MaxWeightKgPerBag <= 0)
            return await req.BadRequestAsync("maxWeightKgPerBag must be > 0.");

        var command = BagMapper.ToCommand(policyId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No policy found for ID '{policyId}'.");

        return await req.OkJsonAsync(BagMapper.ToResponse(updated));
    }

    [Function("DeleteBagPolicy")]
    [OpenApiOperation(operationId: "DeleteBagPolicy", tags: new[] { "BagPolicies" }, Summary = "Delete a bag policy")]
    [OpenApiParameter(name: "policyId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag policy ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteBagPolicyCommand(policyId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No policy found for ID '{policyId}'.");
    }
}
