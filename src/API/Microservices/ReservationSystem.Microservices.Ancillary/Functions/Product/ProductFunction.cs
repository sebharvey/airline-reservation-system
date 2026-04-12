using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductPrice;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductPrice;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProducts;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductPrice;
using ReservationSystem.Microservices.Ancillary.Models.Product.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Product.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Product.Responses;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Microservices.Ancillary.Functions.Product;

public sealed class ProductFunction
{
    private readonly GetProductHandler _getHandler;
    private readonly GetAllProductsHandler _getAllHandler;
    private readonly CreateProductHandler _createHandler;
    private readonly UpdateProductHandler _updateHandler;
    private readonly DeleteProductHandler _deleteHandler;
    private readonly CreateProductPriceHandler _createPriceHandler;
    private readonly UpdateProductPriceHandler _updatePriceHandler;
    private readonly DeleteProductPriceHandler _deletePriceHandler;
    private readonly ILogger<ProductFunction> _logger;

    public ProductFunction(
        GetProductHandler getHandler,
        GetAllProductsHandler getAllHandler,
        CreateProductHandler createHandler,
        UpdateProductHandler updateHandler,
        DeleteProductHandler deleteHandler,
        CreateProductPriceHandler createPriceHandler,
        UpdateProductPriceHandler updatePriceHandler,
        DeleteProductPriceHandler deletePriceHandler,
        ILogger<ProductFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _createPriceHandler = createPriceHandler;
        _updatePriceHandler = updatePriceHandler;
        _deletePriceHandler = deletePriceHandler;
        _logger = logger;
    }

    // ── Product CRUD ───────────────────────────────────────────────────────────

    [Function("GetAllProducts")]
    [OpenApiOperation(operationId: "GetAllProducts", tags: new[] { "Product" }, Summary = "List all products")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductListResponse), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var products = await _getAllHandler.HandleAsync(new GetAllProductsQuery(), cancellationToken);
        return await req.OkJsonAsync(new { products = ProductMapper.ToResponse(products) });
    }

    [Function("GetProduct")]
    [OpenApiOperation(operationId: "GetProduct", tags: new[] { "Product" }, Summary = "Get a product by ID")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await _getHandler.HandleAsync(new GetProductQuery(productId), cancellationToken);
        if (product is null)
            return await req.NotFoundAsync($"No product found for ID '{productId}'.");
        return await req.OkJsonAsync(ProductMapper.ToResponse(product));
    }

    [Function("CreateProduct")]
    [OpenApiOperation(operationId: "CreateProduct", tags: new[] { "Product" }, Summary = "Create a product")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateProductRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateProductRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("name is required.");

        if (request.ProductGroupId == Guid.Empty)
            return await req.BadRequestAsync("productGroupId is required.");

        if (!string.IsNullOrWhiteSpace(request.SsrCode) && request.SsrCode.Length != 4)
            return await req.BadRequestAsync("ssrCode must be exactly 4 characters.");

        var command = ProductMapper.ToCommand(request);
        var created = await _createHandler.HandleAsync(command, cancellationToken);
        return await req.CreatedAsync($"/v1/products/{created.ProductId}", ProductMapper.ToResponse(created));
    }

    [Function("UpdateProduct")]
    [OpenApiOperation(operationId: "UpdateProduct", tags: new[] { "Product" }, Summary = "Update a product")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateProductRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateProductRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("name is required.");

        if (!string.IsNullOrWhiteSpace(request.SsrCode) && request.SsrCode.Length != 4)
            return await req.BadRequestAsync("ssrCode must be exactly 4 characters.");

        var command = ProductMapper.ToCommand(productId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No product found for ID '{productId}'.");

        return await req.OkJsonAsync(ProductMapper.ToResponse(updated));
    }

    [Function("DeleteProduct")]
    [OpenApiOperation(operationId: "DeleteProduct", tags: new[] { "Product" }, Summary = "Delete a product")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteProductCommand(productId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No product found for ID '{productId}'.");
    }

    // ── ProductPrice CRUD ──────────────────────────────────────────────────────

    [Function("CreateProductPrice")]
    [OpenApiOperation(operationId: "CreateProductPrice", tags: new[] { "Product" }, Summary = "Add a currency price to a product")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateProductPriceRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductPriceResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — currency already has a price for this product")]
    public async Task<HttpResponseData> CreatePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/products/{productId:guid}/prices")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateProductPriceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Length != 3)
            return await req.BadRequestAsync("currencyCode must be a 3-character ISO 4217 code.");

        if (request.Price < 0)
            return await req.BadRequestAsync("price must be >= 0.");

        if (request.Tax < 0)
            return await req.BadRequestAsync("tax must be >= 0.");

        try
        {
            var command = ProductMapper.ToCommand(productId, request);
            var created = await _createPriceHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync(
                $"/v1/products/{productId}/prices/{created.PriceId}",
                ProductMapper.ToResponse(created));
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("UQ_ProductPrice_ProductCurrency") == true
                                   || ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
        {
            return await req.ConflictAsync(
                $"A price for currency '{request.CurrencyCode}' already exists for this product.");
        }
    }

    [Function("UpdateProductPrice")]
    [OpenApiOperation(operationId: "UpdateProductPrice", tags: new[] { "Product" }, Summary = "Update a product price")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "priceId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateProductPriceRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductPriceResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/products/{productId:guid}/prices/{priceId:guid}")] HttpRequestData req,
        Guid productId,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateProductPriceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.Price < 0)
            return await req.BadRequestAsync("price must be >= 0.");

        if (request.Tax < 0)
            return await req.BadRequestAsync("tax must be >= 0.");

        var command = ProductMapper.ToCommand(priceId, request);
        var updated = await _updatePriceHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No price found for ID '{priceId}'.");

        return await req.OkJsonAsync(ProductMapper.ToResponse(updated));
    }

    [Function("DeleteProductPrice")]
    [OpenApiOperation(operationId: "DeleteProductPrice", tags: new[] { "Product" }, Summary = "Delete a product price")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "priceId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeletePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/products/{productId:guid}/prices/{priceId:guid}")] HttpRequestData req,
        Guid productId,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deletePriceHandler.HandleAsync(new DeleteProductPriceCommand(priceId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No price found for ID '{priceId}'.");
    }
}
