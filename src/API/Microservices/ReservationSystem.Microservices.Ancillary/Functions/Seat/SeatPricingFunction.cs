using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatPricings;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Ancillary.Functions.Seat;

public sealed class SeatPricingFunction
{
    private readonly GetAllSeatPricingsHandler _getAllHandler;
    private readonly CreateSeatPricingHandler _createHandler;
    private readonly GetSeatPricingHandler _getHandler;
    private readonly UpdateSeatPricingHandler _updateHandler;
    private readonly DeleteSeatPricingHandler _deleteHandler;
    private readonly ILogger<SeatPricingFunction> _logger;

    public SeatPricingFunction(
        GetAllSeatPricingsHandler getAllHandler,
        CreateSeatPricingHandler createHandler,
        GetSeatPricingHandler getHandler,
        UpdateSeatPricingHandler updateHandler,
        DeleteSeatPricingHandler deleteHandler,
        ILogger<SeatPricingFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _getHandler = getHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    [Function("GetAllSeatPricings")]
    [OpenApiOperation(operationId: "GetAllSeatPricings", tags: new[] { "SeatPricing" }, Summary = "List all seat pricing rules")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatPricingResponse[]), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var pricings = await _getAllHandler.HandleAsync(new GetAllSeatPricingsQuery(), cancellationToken);
        return await req.OkJsonAsync(new { pricing = SeatMapper.ToResponse(pricings) });
    }

    [Function("CreateSeatPricing")]
    [OpenApiOperation(operationId: "CreateSeatPricing", tags: new[] { "SeatPricing" }, Summary = "Create a new seat pricing rule")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateSeatPricingRequest), Required = true, Description = "The seat pricing rule to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(SeatPricingResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateSeatPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.CabinCode) || request.CabinCode is not ("W" or "Y"))
            return await req.BadRequestAsync("cabinCode must be 'W' or 'Y'. Business (J) and First (F) carry no ancillary charge.");

        if (string.IsNullOrWhiteSpace(request.SeatPosition) || request.SeatPosition is not ("Window" or "Aisle" or "Middle"))
            return await req.BadRequestAsync("seatPosition must be 'Window', 'Aisle', or 'Middle'.");

        if (request.Price <= 0)
            return await req.BadRequestAsync("price must be > 0.");

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            return await req.BadRequestAsync("currencyCode is required.");

        if (request.ValidTo.HasValue && request.ValidFrom > request.ValidTo.Value)
            return await req.BadRequestAsync("validFrom must not be after validTo.");

        try
        {
            var command = SeatMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            var response = SeatMapper.ToResponse(created);
            return await req.CreatedAsync($"/v1/seat-pricing/{created.SeatPricingId}", response);
        }
        catch (Exception ex) when (ex.Message.Contains("UQ_SeatPricing") || ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
        {
            return await req.ConflictAsync(
                $"A pricing rule already exists for {request.CabinCode}/{request.SeatPosition}/{request.CurrencyCode}.");
        }
    }

    [Function("GetSeatPricing")]
    [OpenApiOperation(operationId: "GetSeatPricing", tags: new[] { "SeatPricing" }, Summary = "Get a seat pricing rule by ID")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seat pricing rule ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatPricingResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var pricing = await _getHandler.HandleAsync(new GetSeatPricingQuery(seatPricingId), cancellationToken);
        if (pricing is null)
            return await req.NotFoundAsync($"No pricing rule found for ID '{seatPricingId}'.");
        return await req.OkJsonAsync(SeatMapper.ToResponse(pricing));
    }

    [Function("UpdateSeatPricing")]
    [OpenApiOperation(operationId: "UpdateSeatPricing", tags: new[] { "SeatPricing" }, Summary = "Update a seat pricing rule")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seat pricing rule ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSeatPricingRequest), Required = true, Description = "The update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatPricingResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateSeatPricingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request.Price.HasValue && request.Price.Value <= 0)
            return await req.BadRequestAsync("price must be > 0.");

        if (request.ValidTo.HasValue && request.ValidFrom.HasValue && request.ValidFrom > request.ValidTo)
            return await req.BadRequestAsync("validFrom must not be after validTo.");

        var command = SeatMapper.ToCommand(seatPricingId, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No pricing rule found for ID '{seatPricingId}'.");

        return await req.OkJsonAsync(SeatMapper.ToResponse(updated));
    }

    [Function("DeleteSeatPricing")]
    [OpenApiOperation(operationId: "DeleteSeatPricing", tags: new[] { "SeatPricing" }, Summary = "Delete a seat pricing rule")]
    [OpenApiParameter(name: "seatPricingId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seat pricing rule ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteSeatPricingCommand(seatPricingId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No pricing rule found for ID '{seatPricingId}'.");
    }
}
