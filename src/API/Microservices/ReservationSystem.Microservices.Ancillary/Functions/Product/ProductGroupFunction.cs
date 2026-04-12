using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProductGroups;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductGroup;
using ReservationSystem.Microservices.Ancillary.Models.Product.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Product.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Product.Responses;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Microservices.Ancillary.Functions.Product;

public sealed class ProductGroupFunction
{
    private readonly GetProductGroupHandler _getHandler;
    private readonly GetAllProductGroupsHandler _getAllHandler;
    private readonly CreateProductGroupHandler _createHandler;
    private readonly UpdateProductGroupHandler _updateHandler;
    private readonly DeleteProductGroupHandler _deleteHandler;
    private readonly ILogger<ProductGroupFunction> _logger;

    public ProductGroupFunction(
        GetProductGroupHandler getHandler,
        GetAllProductGroupsHandler getAllHandler,
        CreateProductGroupHandler createHandler,
        UpdateProductGroupHandler updateHandler,
        DeleteProductGroupHandler deleteHandler,
        ILogger<ProductGroupFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    [Function("GetAllProductGroups")]
    [OpenApiOperation(operationId: "GetAllProductGroups", tags: new[] { "ProductGroup" }, Summary = "List all product groups")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductGroupListResponse), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/product-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var groups = await _getAllHandler.HandleAsync(new GetAllProductGroupsQuery(), cancellationToken);
        return await req.OkJsonAsync(new { groups = ProductMapper.ToResponse(groups) });
    }

    [Function("GetProductGroup")]
    [OpenApiOperation(operationId: "GetProductGroup", tags: new[] { "ProductGroup" }, Summary = "Get a product group by ID")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductGroupResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await _getHandler.HandleAsync(new GetProductGroupQuery(groupId), cancellationToken);
        if (group is null)
            return await req.NotFoundAsync($"No product group found for ID '{groupId}'.");
        return await req.OkJsonAsync(ProductMapper.ToResponse(group));
    }

    [Function("CreateProductGroup")]
    [OpenApiOperation(operationId: "CreateProductGroup", tags: new[] { "ProductGroup" }, Summary = "Create a product group")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateProductGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(ProductGroupResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/product-groups")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateProductGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("name is required.");

        try
        {
            var command = ProductMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/product-groups/{created.ProductGroupId}", ProductMapper.ToResponse(created));
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("UQ_ProductGroup_Name") == true
                                   || ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
        {
            return await req.ConflictAsync($"A product group with the name '{request.Name}' already exists.");
        }
    }

    [Function("UpdateProductGroup")]
    [OpenApiOperation(operationId: "UpdateProductGroup", tags: new[] { "ProductGroup" }, Summary = "Update a product group")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateProductGroupRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProductGroupResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateProductGroupRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Name))
            return await req.BadRequestAsync("name is required.");

        var command = ProductMapper.ToCommand(groupId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No product group found for ID '{groupId}'.");

        return await req.OkJsonAsync(ProductMapper.ToResponse(updated));
    }

    [Function("DeleteProductGroup")]
    [OpenApiOperation(operationId: "DeleteProductGroup", tags: new[] { "ProductGroup" }, Summary = "Delete a product group")]
    [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/product-groups/{groupId:guid}")] HttpRequestData req,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteProductGroupCommand(groupId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No product group found for ID '{groupId}'.");
    }
}
