using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class AdminProductFunction
{
    private readonly ProductServiceClient _productServiceClient;
    private readonly ILogger<AdminProductFunction> _logger;

    public AdminProductFunction(ProductServiceClient productServiceClient, ILogger<AdminProductFunction> logger)
    {
        _productServiceClient = productServiceClient;
        _logger = logger;
    }

    // ── Product CRUD ───────────────────────────────────────────────────────────

    [Function("AdminGetAllProducts")]
    [OpenApiOperation(operationId: "AdminGetAllProducts", tags: new[] { "Admin Products" }, Summary = "List all products (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<ProductDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _productServiceClient.GetAllProductsAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list products");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminGetProduct")]
    [OpenApiOperation(operationId: "AdminGetProduct", tags: new[] { "Admin Products" }, Summary = "Get a product by ID (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productServiceClient.GetProductAsync(productId, cancellationToken);
            if (product is null)
                return await req.NotFoundAsync($"Product '{productId}' not found.");
            return await req.OkJsonAsync(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product {ProductId}", productId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminCreateProduct")]
    [OpenApiOperation(operationId: "AdminCreateProduct", tags: new[] { "Admin Products" }, Summary = "Create a product (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateProductRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateProductRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.CreateProductAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/products/{result.ProductId}", result);
        }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateProduct")]
    [OpenApiOperation(operationId: "AdminUpdateProduct", tags: new[] { "Admin Products" }, Summary = "Update a product (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateProductRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateProductRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.UpdateProductAsync(productId, request!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product {ProductId}", productId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteProduct")]
    [OpenApiOperation(operationId: "AdminDeleteProduct", tags: new[] { "Admin Products" }, Summary = "Delete a product (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/products/{productId:guid}")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _productServiceClient.DeleteProductAsync(productId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Product '{productId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product {ProductId}", productId);
            return await req.InternalServerErrorAsync();
        }
    }

    // ── ProductPrice CRUD ──────────────────────────────────────────────────────

    [Function("AdminCreateProductPrice")]
    [OpenApiOperation(operationId: "AdminCreateProductPrice", tags: new[] { "Admin Products" }, Summary = "Add a currency price to a product (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateProductPriceRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductPriceDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> CreatePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/products/{productId:guid}/prices")] HttpRequestData req,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateProductPriceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.CreateProductPriceAsync(productId, request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/products/{productId}/prices/{result.PriceId}", result);
        }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.ConflictAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product price for product {ProductId}", productId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateProductPrice")]
    [OpenApiOperation(operationId: "AdminUpdateProductPrice", tags: new[] { "Admin Products" }, Summary = "Update a product price (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "priceId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateProductPriceRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductPriceDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/products/{productId:guid}/prices/{priceId:guid}")] HttpRequestData req,
        Guid productId,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateProductPriceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.UpdateProductPriceAsync(productId, priceId, request!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product price {PriceId}", priceId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteProductPrice")]
    [OpenApiOperation(operationId: "AdminDeleteProductPrice", tags: new[] { "Admin Products" }, Summary = "Delete a product price (staff)")]
    [OpenApiParameter(name: "productId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "priceId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeletePrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/products/{productId:guid}/prices/{priceId:guid}")] HttpRequestData req,
        Guid productId,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _productServiceClient.DeleteProductPriceAsync(productId, priceId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Product price '{priceId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product price {PriceId}", priceId);
            return await req.InternalServerErrorAsync();
        }
    }
}
