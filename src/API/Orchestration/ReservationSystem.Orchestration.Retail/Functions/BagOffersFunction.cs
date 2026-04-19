using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Net;
using System.Web;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for bag offer retrieval.
/// Proxies bag policy and priced offers from the Ancillary microservice, adapting
/// the response into the channel-facing shape expected by the web application.
/// </summary>
public sealed class BagOffersFunction
{
    private readonly BagServiceClient _bagServiceClient;
    private readonly ILogger<BagOffersFunction> _logger;

    public BagOffersFunction(BagServiceClient bagServiceClient, ILogger<BagOffersFunction> logger)
    {
        _bagServiceClient = bagServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/bags/offers?inventoryId=&cabinCode=
    // -------------------------------------------------------------------------

    [Function("GetBagOffers")]
    [OpenApiOperation(operationId: "GetBagOffers", tags: new[] { "Bags" }, Summary = "Get bag policy and priced bag offers for a flight cabin")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The inventory (flight) ID")]
    [OpenApiParameter(name: "cabinCode", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The cabin code (F, J, W, or Y)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Bag policy and priced offers")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetBagOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bags/offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var inventoryId = query["inventoryId"];
        var cabinCode = query["cabinCode"];

        if (string.IsNullOrWhiteSpace(inventoryId))
            return await req.BadRequestAsync("'inventoryId' query parameter is required.");

        if (string.IsNullOrWhiteSpace(cabinCode))
            return await req.BadRequestAsync("'cabinCode' query parameter is required.");

        var bagOffers = await _bagServiceClient.GetBagOffersAsync(inventoryId, cabinCode, cancellationToken);

        if (bagOffers is null)
            return await req.NotFoundAsync($"No bag offers found for inventory '{inventoryId}' and cabin '{cabinCode}'.");

        // Map to channel-facing shape expected by the Angular web app
        var response = new
        {
            policy = new
            {
                cabinCode = bagOffers.CabinCode,
                freeBagsIncluded = bagOffers.Policy?.FreeBagsIncluded ?? 0,
                maxWeightKgPerBag = bagOffers.Policy?.MaxWeightKgPerBag ?? 23
            },
            additionalBagOffers = bagOffers.BagOffers.Select(o => new
            {
                bagOfferId = o.BagOfferId,
                bagSequence = o.BagSequence,
                price = o.Price,
                tax = o.Tax,
                currency = o.CurrencyCode,
                label = o.Description
            }).ToList()
        };

        return await req.OkJsonAsync(response);
    }
}
