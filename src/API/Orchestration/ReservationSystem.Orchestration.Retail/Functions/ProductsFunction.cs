using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered function for retail product catalogue retrieval.
/// Fetches products and product groups from the Ancillary microservice in
/// parallel, then returns the catalogue pre-grouped as productGroups[] →
/// products[] so channels receive a ready-to-render structure.
/// </summary>
public sealed class ProductsFunction
{
    private readonly ProductServiceClient _productServiceClient;
    private readonly ILogger<ProductsFunction> _logger;

    public ProductsFunction(ProductServiceClient productServiceClient, ILogger<ProductsFunction> logger)
    {
        _productServiceClient = productServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/products
    // -------------------------------------------------------------------------

    [Function("GetRetailProducts")]
    [OpenApiOperation(operationId: "GetRetailProducts", tags: new[] { "Products" }, Summary = "List all active retail products grouped by product group, with per-currency prices")]
    [OpenApiParameter(name: "channel", In = Microsoft.OpenApi.Models.ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Filter to products available on this channel (WEB, APP, NDC, KIOSK, CC, AIRPORT)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Products with prices; channel filters by basket currency and groups for display")]
    public async Task<HttpResponseData> GetProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Optional channel filter — when supplied, only products available on that channel are returned
        var channelFilter = req.Query["channel"]?.Trim().ToUpperInvariant();

        // Fetch products and product groups in parallel
        var productsTask = _productServiceClient.GetAllProductsAsync(cancellationToken);
        var groupsTask   = _productServiceClient.GetAllProductGroupsAsync(cancellationToken);

        await Task.WhenAll(productsTask, groupsTask);

        var productList = await productsTask;
        var groupList   = await groupsTask;

        if (productList is null || productList.Products.Count == 0)
            return await req.OkJsonAsync(new { productGroups = Array.Empty<object>() });

        // Build a lookup of groupId → group name (active groups only)
        var groupNames = groupList?.Groups
            .Where(g => g.IsActive)
            .ToDictionary(g => g.ProductGroupId, g => g.Name)
            ?? new Dictionary<Guid, string>();

        // Group active products by their product group, preserving API order;
        // apply channel filter when provided
        var grouped = productList.Products
            .Where(p => p.IsActive)
            .Where(p => channelFilter is null ||
                        (System.Text.Json.JsonSerializer.Deserialize<string[]>(p.AvailableChannels) ?? [])
                         .Any(c => c.Equals(channelFilter, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(p => p.ProductGroupId)
            .Select(g => new
            {
                productGroupId   = g.Key,
                productGroupName = groupNames.TryGetValue(g.Key, out var gn) ? gn : string.Empty,
                products         = g.Select(p => new
                {
                    productId         = p.ProductId,
                    name              = p.Name,
                    description       = p.Description,
                    imageBase64       = p.ImageBase64,
                    ssrCode           = p.SsrCode,
                    isSegmentSpecific = p.IsSegmentSpecific,
                    prices            = p.Prices
                        .Where(pr => pr.IsActive)
                        .Select(pr => new
                        {
                            priceId      = pr.PriceId,
                            offerId      = pr.OfferId,
                            currencyCode = pr.CurrencyCode,
                            price        = pr.Price,
                            tax          = pr.Tax
                        })
                        .ToList()
                }).ToList()
            })
            .ToList();

        return await req.OkJsonAsync(new { productGroups = grouped });
    }
}
