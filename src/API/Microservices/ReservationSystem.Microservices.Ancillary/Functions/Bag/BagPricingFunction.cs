using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.DeleteBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPricings;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPricing;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Bag.Responses;
using ReservationSystem.Shared.Common.Http;

namespace ReservationSystem.Microservices.Ancillary.Functions.Bag;

public sealed class BagPricingFunction
{
    private readonly GetBagPricingHandler _getHandler;
    private readonly GetAllBagPricingsHandler _getAllHandler;
    private readonly CreateBagPricingHandler _createHandler;
    private readonly UpdateBagPricingHandler _updateHandler;
    private readonly DeleteBagPricingHandler _deleteHandler;
    private readonly ILogger<BagPricingFunction> _logger;

    public BagPricingFunction(
        GetBagPricingHandler getHandler,
        GetAllBagPricingsHandler getAllHandler,
        CreateBagPricingHandler createHandler,
        UpdateBagPricingHandler updateHandler,
        DeleteBagPricingHandler deleteHandler,
        ILogger<BagPricingFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    [Function("GetAllBagPricings")]
    [OpenApiOperation(operationId: "GetAllBagPricings", tags: new[] { "BagPricing" }, Summary = "List all bag pricing rules")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPricingListResponse), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var pricings = await _getAllHandler.HandleAsync(new GetAllBagPricingsQuery(), cancellationToken);
        var body = new { pricing = BagMapper.ToResponse(pricings) };
        return await req.OkJsonAsync(body);
    }

    [Function("GetBagPricing")]
    [OpenApiOperation(operationId: "GetBagPricing", tags: new[] { "BagPricing" }, Summary = "Get a bag pricing rule by ID")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag pricing rule ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPricingResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        var pricing = await _getHandler.HandleAsync(new GetBagPricingQuery(pricingId), cancellationToken);
        if (pricing is null)
            return await req.NotFoundAsync($"No pricing rule found for ID '{pricingId}'.");
        return await req.OkJsonAsync(BagMapper.ToResponse(pricing));
    }

    [Function("CreateBagPricing")]
    [OpenApiOperation(operationId: "CreateBagPricing", tags: new[] { "BagPricing" }, Summary = "Create a new bag pricing rule")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBagPricingRequest), Required = true, Description = "The bag pricing rule to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BagPricingResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateBagPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.BagSequence is not (1 or 2 or 99))
            return await req.BadRequestAsync("bagSequence must be 1, 2, or 99.");

        if (request.Price <= 0)
            return await req.BadRequestAsync("price must be > 0.");

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            return await req.BadRequestAsync("currencyCode is required.");

        if (request.ValidTo.HasValue && request.ValidFrom > request.ValidTo.Value)
            return await req.BadRequestAsync("validFrom must not be after validTo.");

        try
        {
            var command = BagMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            var response = BagMapper.ToResponse(created);
            return await req.CreatedAsync($"/v1/bag-pricing/{created.PricingId}", response);
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("UQ_BagPricing_Sequence") == true
                                   || ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
        {
            return await req.ConflictAsync(
                $"A pricing rule already exists for sequence {request.BagSequence} and currency '{request.CurrencyCode}'.");
        }
    }

    [Function("UpdateBagPricing")]
    [OpenApiOperation(operationId: "UpdateBagPricing", tags: new[] { "BagPricing" }, Summary = "Update a bag pricing rule")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag pricing rule ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateBagPricingRequest), Required = true, Description = "The update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BagPricingResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateBagPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.Price <= 0)
            return await req.BadRequestAsync("price must be > 0.");

        if (request.ValidTo.HasValue && request.ValidFrom > request.ValidTo.Value)
            return await req.BadRequestAsync("validFrom must not be after validTo.");

        var command = BagMapper.ToCommand(pricingId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No pricing rule found for ID '{pricingId}'.");

        return await req.OkJsonAsync(BagMapper.ToResponse(updated));
    }

    [Function("DeleteBagPricing")]
    [OpenApiOperation(operationId: "DeleteBagPricing", tags: new[] { "BagPricing" }, Summary = "Delete a bag pricing rule")]
    [OpenApiParameter(name: "pricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The bag pricing rule ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteBagPricingCommand(pricingId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No pricing rule found for ID '{pricingId}'.");
    }
}
