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

namespace ReservationSystem.Microservices.Bags.Functions;

/// <summary>
/// HTTP-triggered functions for the BagPolicy resource.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// All endpoints are boilerplate scaffolds; implementation is pending.
/// </summary>
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

    // -------------------------------------------------------------------------
    // GET /v1/bag-policies
    // -------------------------------------------------------------------------

    [Function("GetAllBagPolicies")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/bag-policies/{policyId:guid}
    // -------------------------------------------------------------------------

    [Function("GetBagPolicy")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/bag-policies
    // -------------------------------------------------------------------------

    [Function("CreateBagPolicy")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-policies")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/bag-policies/{policyId:guid}
    // -------------------------------------------------------------------------

    [Function("UpdateBagPolicy")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/bag-policies/{policyId:guid}
    // -------------------------------------------------------------------------

    [Function("DeleteBagPolicy")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/bag-policies/{policyId:guid}")] HttpRequestData req,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
