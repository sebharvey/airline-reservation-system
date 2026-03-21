using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ReservationSystem.Microservices.Bags.Functions;

/// <summary>
/// HTTP-triggered functions for bag offer generation.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// All endpoints are boilerplate scaffolds; implementation is pending.
/// </summary>
public sealed class BagOfferFunction
{
    private readonly ILogger<BagOfferFunction> _logger;

    public BagOfferFunction(ILogger<BagOfferFunction> logger)
    {
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/bags/offers?inventoryId={guid}&cabinCode={string}
    // -------------------------------------------------------------------------

    [Function("GetBagOffers")]
    public async Task<HttpResponseData> GetBagOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bags/offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/bags/offers/{bagOfferId}
    // -------------------------------------------------------------------------

    [Function("GetBagOffer")]
    public async Task<HttpResponseData> GetBagOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bags/offers/{bagOfferId}")] HttpRequestData req,
        string bagOfferId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
