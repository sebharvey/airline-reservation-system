using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Web;

namespace ReservationSystem.Microservices.Ancillary.Functions.Bag;

public sealed class BagOfferFunction
{
    private readonly IBagPolicyRepository _policyRepository;
    private readonly IBagPricingRepository _pricingRepository;
    private readonly ILogger<BagOfferFunction> _logger;

    public BagOfferFunction(
        IBagPolicyRepository policyRepository,
        IBagPricingRepository pricingRepository,
        ILogger<BagOfferFunction> logger)
    {
        _policyRepository = policyRepository;
        _pricingRepository = pricingRepository;
        _logger = logger;
    }

    /// <summary>
    /// GET /v1/bags/offers?inventoryId={inventoryId}&amp;cabinCode={cabinCode}
    /// Generate and return the free bag policy and priced bag offers for a flight and cabin.
    /// BagOfferId values are deterministic: bo-{first8chars_of_inventoryId}-{cabinCode}-{bagSequence}-v1
    /// </summary>
    [Function("GetBagOffers")]
    [OpenApiOperation(operationId: "GetBagOffers", tags: new[] { "BagOffers" }, Summary = "Get bag offers for a flight and cabin")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The inventory (flight) ID")]
    [OpenApiParameter(name: "cabinCode", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The cabin code (F, J, W, or Y)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagOffersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetBagOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bags/offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var inventoryIdStr = query["inventoryId"];
        var cabinCode = query["cabinCode"];

        if (string.IsNullOrWhiteSpace(inventoryIdStr) || !Guid.TryParse(inventoryIdStr, out var inventoryId))
            return await req.BadRequestAsync("Missing or invalid 'inventoryId' query parameter.");

        if (string.IsNullOrWhiteSpace(cabinCode) || cabinCode is not ("F" or "J" or "W" or "Y"))
            return await req.BadRequestAsync("Missing or invalid 'cabinCode' query parameter. Must be F, J, W, or Y.");

        var policy = await _policyRepository.GetByCabinCodeAsync(cabinCode, cancellationToken);
        if (policy is null || !policy.IsActive)
            return await req.NotFoundAsync($"No active bag policy found for cabin '{cabinCode}'.");

        var activePricings = await _pricingRepository.GetAllActiveAsync(cancellationToken);

        var inventoryIdPrefix = inventoryId.ToString("N")[..8];

        var bagOffers = activePricings.Select(p => new BagOfferItem
        {
            BagOfferId = $"bo-{inventoryIdPrefix}-{cabinCode}-{p.BagSequence}-v1",
            BagSequence = p.BagSequence,
            Description = BagMapper.GetBagDescription(p.BagSequence),
            Price = p.Price,
            CurrencyCode = p.CurrencyCode
        }).ToList();

        var response = new BagOffersResponse
        {
            InventoryId = inventoryId,
            CabinCode = cabinCode,
            Policy = new BagPolicyInfo
            {
                FreeBagsIncluded = policy.FreeBagsIncluded,
                MaxWeightKgPerBag = policy.MaxWeightKgPerBag
            },
            BagOffers = bagOffers
        };

        return await req.OkJsonAsync(response);
    }

    /// <summary>
    /// GET /v1/bags/offers/{bagOfferId}
    /// Validate a bag offer by its deterministic ID. Resolves to constituent parts and checks pricing is still active.
    /// BagOfferId format: bo-{inventoryIdPrefix}-{cabinCode}-{bagSequence}-v1
    /// </summary>
    [Function("GetBagOffer")]
    [OpenApiOperation(operationId: "GetBagOffer", tags: new[] { "BagOffers" }, Summary = "Validate a specific bag offer")]
    [OpenApiParameter(name: "bagOfferId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The bag offer ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagOfferValidationResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetBagOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bags/offers/{bagOfferId}")] HttpRequestData req,
        string bagOfferId,
        CancellationToken cancellationToken)
    {
        // Parse the deterministic bagOfferId: bo-{8chars}-{cabinCode}-{sequence}-v1
        var parts = bagOfferId.Split('-');
        if (parts.Length < 4 || parts[0] != "bo")
            return await req.BadRequestAsync($"Malformed bagOfferId: '{bagOfferId}'.");

        // Format: bo-{inventoryPrefix}-{cabinCode}-{bagSequence}-v1
        // parts[0]=bo, parts[1]=inventoryPrefix, parts[2]=cabinCode, parts[3]=bagSequence, parts[4]=v1
        if (parts.Length < 5)
            return await req.BadRequestAsync($"Malformed bagOfferId: '{bagOfferId}'.");

        var inventoryPrefix = parts[1];
        var cabinCode = parts[2];
        if (!int.TryParse(parts[3], out var bagSequence))
            return await req.BadRequestAsync($"Malformed bagOfferId: cannot parse bag sequence from '{bagOfferId}'.");

        if (cabinCode is not ("F" or "J" or "W" or "Y"))
            return await req.BadRequestAsync($"Malformed bagOfferId: invalid cabin code in '{bagOfferId}'.");

        // Find the active pricing rule for this sequence
        var pricing = await _pricingRepository.GetBySequenceAsync(bagSequence, "GBP", cancellationToken);
        if (pricing is null || !pricing.IsActive)
            return await req.NotFoundAsync($"The pricing rule underlying offer '{bagOfferId}' is no longer active.");

        // Reconstruct a placeholder inventoryId from the prefix (for the response)
        // We pad with zeros to create a valid GUID-like format
        var paddedId = inventoryPrefix.PadRight(32, '0');
        var inventoryId = Guid.Parse(paddedId[..8] + "-" + paddedId[8..12] + "-" + paddedId[12..16] + "-" + paddedId[16..20] + "-" + paddedId[20..32]);

        var response = new BagOfferValidationResponse
        {
            BagOfferId = bagOfferId,
            InventoryId = inventoryId,
            CabinCode = cabinCode,
            BagSequence = bagSequence,
            Description = BagMapper.GetBagDescription(bagSequence),
            Price = pricing.Price,
            CurrencyCode = pricing.CurrencyCode,
            IsValid = true
        };

        return await req.OkJsonAsync(response);
    }
}
