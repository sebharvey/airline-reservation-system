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

public sealed class AdminProductGroupFunction
{
    private readonly ProductServiceClient _productServiceClient;
    private readonly ILogger<AdminProductGroupFunction> _logger;

    public AdminProductGroupFunction(ProductServiceClient productServiceClient, ILogger<AdminProductGroupFunction> logger)
    {
        _productServiceClient = productServiceClient;
        _logger = logger;
    }

    [Function("AdminGetAllProductGroups")]
    [OpenApiOperation(operationId: "AdminGetAllProductGroups", tags: new[] { "Admin Product Groups" }, Summary = "List all product groups (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<ProductGroupDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/product-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _productServiceClient.GetAllProductGroupsAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list product groups");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminGetProductGroup")]
    [OpenApiOperation(operationId: "AdminGetProductGroup", tags: new[] { "Admin Product Groups" }, Summary = "Get a product group by ID (staff)")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductGroupDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _productServiceClient.GetProductGroupAsync(groupId, cancellationToken);
            if (group is null)
                return await req.NotFoundAsync($"Product group '{groupId}' not found.");
            return await req.OkJsonAsync(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product group {GroupId}", groupId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminCreateProductGroup")]
    [OpenApiOperation(operationId: "AdminCreateProductGroup", tags: new[] { "Admin Product Groups" }, Summary = "Create a product group (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateProductGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductGroupDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/product-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateProductGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.CreateProductGroupAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/product-groups/{result.ProductGroupId}", result);
        }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.ConflictAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product group");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateProductGroup")]
    [OpenApiOperation(operationId: "AdminUpdateProductGroup", tags: new[] { "Admin Product Groups" }, Summary = "Update a product group (staff)")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateProductGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductGroupDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateProductGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _productServiceClient.UpdateProductGroupAsync(groupId, request!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product group {GroupId}", groupId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteProductGroup")]
    [OpenApiOperation(operationId: "AdminDeleteProductGroup", tags: new[] { "Admin Product Groups" }, Summary = "Delete a product group (staff)")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _productServiceClient.DeleteProductGroupAsync(groupId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Product group '{groupId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product group {GroupId}", groupId);
            return await req.InternalServerErrorAsync();
        }
    }
}
