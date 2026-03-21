using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Application.CreateBagPricing;
using ReservationSystem.Microservices.Bags.Application.DeleteBagPricing;
using ReservationSystem.Microservices.Bags.Application.GetAllBagPricings;
using ReservationSystem.Microservices.Bags.Application.GetBagPricing;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;
using ReservationSystem.Microservices.Bags.Models.Mappers;
using ReservationSystem.Microservices.Bags.Models.Requests;

namespace ReservationSystem.Microservices.Bags.Functions;

/// <summary>
/// HTTP-triggered functions for the BagPricing resource.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// All endpoints are boilerplate scaffolds; implementation is pending.
/// </summary>
public sealed class BagPricingFunction
{
    private readonly GetBagPricingHandler _getHandler;
    private readonly GetAllBagPricingsHandler _getAllHandler;
    private readonly CreateBagPricingHandler _createHandler;
    private readonly UpdateBagPricingHandler _updateHandler;
    private readonly DeleteBagPricingHandler _deleteHandler;
    private readonly ILogger<BagPricingFunction> _logger;

    public BagPricingFunction(
        GetBagPricingHandler getHandler,
        GetAllBagPricingsHandler getAllHandler,
        CreateBagPricingHandler createHandler,
        UpdateBagPricingHandler updateHandler,
        DeleteBagPricingHandler deleteHandler,
        ILogger<BagPricingFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/bag-pricing
    // -------------------------------------------------------------------------

    [Function("GetAllBagPricings")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/bag-pricing/{pricingId:guid}
    // -------------------------------------------------------------------------

    [Function("GetBagPricing")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/bag-pricing
    // -------------------------------------------------------------------------

    [Function("CreateBagPricing")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/bag-pricing/{pricingId:guid}
    // -------------------------------------------------------------------------

    [Function("UpdateBagPricing")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/bag-pricing/{pricingId:guid}
    // -------------------------------------------------------------------------

    [Function("DeleteBagPricing")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
