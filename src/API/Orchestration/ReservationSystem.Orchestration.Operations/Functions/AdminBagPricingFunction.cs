using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class AdminBagPricingFunction
{
    private readonly BagServiceClient _bagServiceClient;
    private readonly ILogger<AdminBagPricingFunction> _logger;

    public AdminBagPricingFunction(BagServiceClient bagServiceClient, ILogger<AdminBagPricingFunction> logger)
    {
        _bagServiceClient = bagServiceClient;
        _logger = logger;
    }

    [Function("AdminGetAllBagPricing")]
    [OpenApiOperation(operationId: "AdminGetAllBagPricing", tags: new[] { "Admin Bag Pricing" }, Summary = "List all bag pricing rules (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<BagPricingDto>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bagServiceClient.GetAllBagPricingAsync(cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list bag pricing rules");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminGetBagPricing")]
    [OpenApiOperation(operationId: "AdminGetBagPricing", tags: new[] { "Admin Bag Pricing" }, Summary = "Get a bag pricing rule by ID (staff)")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPricingDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pricing = await _bagServiceClient.GetBagPricingAsync(pricingId, cancellationToken);
            if (pricing is null)
                return await req.NotFoundAsync($"Bag pricing rule '{pricingId}' not found.");
            return await req.OkJsonAsync(pricing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bag pricing rule {PricingId}", pricingId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminCreateBagPricing")]
    [OpenApiOperation(operationId: "AdminCreateBagPricing", tags: new[] { "Admin Bag Pricing" }, Summary = "Create a new bag pricing rule (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateBagPricingRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BagPricingDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — sequence and currency already has a pricing rule")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateBagPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _bagServiceClient.CreateBagPricingAsync(request!, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/bag-pricing/{result.PricingId}", result);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bag pricing rule");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateBagPricing")]
    [OpenApiOperation(operationId: "AdminUpdateBagPricing", tags: new[] { "Admin Bag Pricing" }, Summary = "Update a bag pricing rule (staff)")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateBagPricingRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPricingDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateBagPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _bagServiceClient.UpdateBagPricingAsync(pricingId, request!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex)
        {
            return await req.NotFoundAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update bag pricing rule {PricingId}", pricingId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteBagPricing")]
    [OpenApiOperation(operationId: "AdminDeleteBagPricing", tags: new[] { "Admin Bag Pricing" }, Summary = "Delete a bag pricing rule (staff)")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _bagServiceClient.DeleteBagPricingAsync(pricingId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Bag pricing rule '{pricingId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bag pricing rule {PricingId}", pricingId);
            return await req.InternalServerErrorAsync();
        }
    }
}
