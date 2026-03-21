using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.DeleteBagPolicy;
using ReservationSystem.Microservices.Bags.Application.GetAllBagPolicies;
using ReservationSystem.Microservices.Bags.Application.GetBagPolicy;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;
using ReservationSystem.Microservices.Bags.Models.Mappers;
using ReservationSystem.Microservices.Bags.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Bags.Functions;

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
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var policies = await _getAllHandler.HandleAsync(new GetAllBagPoliciesQuery(), cancellationToken);
        var body = new { policies = BagMapper.ToResponse(policies) };
        return await req.OkJsonAsync(body);
    }

    [Function("GetBagPolicy")]
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
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateBagPolicyRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateBagPolicyRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateBagPolicy request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CabinCode))
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
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        UpdateBagPolicyRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBagPolicyRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBagPolicy request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (request.MaxWeightKgPerBag <= 0)
            return await req.BadRequestAsync("maxWeightKgPerBag must be > 0.");

        var command = BagMapper.ToCommand(policyId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No policy found for ID '{policyId}'.");

        return await req.OkJsonAsync(BagMapper.ToResponse(updated));
    }

    [Function("DeleteBagPolicy")]
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
