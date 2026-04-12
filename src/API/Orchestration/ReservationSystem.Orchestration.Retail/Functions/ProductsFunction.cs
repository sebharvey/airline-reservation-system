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
/// Proxies all active products (with prices) from the Ancillary microservice,
/// returning the full list so the channel can filter by basket currency and
/// group by product group for display.
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
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Products with prices; channel filters by basket currency and groups for display")]
    public async Task<HttpResponseData> GetProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Fetch products and product groups in parallel
        var productsTask = _productServiceClient.GetAllProductsAsync(cancellationToken);
        var groupsTask   = _productServiceClient.GetAllProductGroupsAsync(cancellationToken);

        await Task.WhenAll(productsTask, groupsTask);

        var productList = await productsTask;
        var groupList   = await groupsTask;

        if (productList is null || productList.Products.Count == 0)
            return await req.OkJsonAsync(new { products = Array.Empty<object>() });

        // Build a lookup of groupId → group name
        var groupNames = groupList?.Groups
            .Where(g => g.IsActive)
            .ToDictionary(g => g.ProductGroupId, g => g.Name)
            ?? new Dictionary<Guid, string>();

        // Return active products with their active prices; include the group name
        var activeProducts = productList.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                productId         = p.ProductId,
                productGroupId    = p.ProductGroupId,
                productGroupName  = groupNames.TryGetValue(p.ProductGroupId, out var gn) ? gn : string.Empty,
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
            })
            .ToList();

        return await req.OkJsonAsync(new { products = activeProducts });
    }
}
