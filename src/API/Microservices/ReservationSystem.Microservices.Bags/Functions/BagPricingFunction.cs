using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Application.CreateBagPricing;
using ReservationSystem.Microservices.Bags.Application.DeleteBagPricing;
using ReservationSystem.Microservices.Bags.Application.GetAllBagPricings;
using ReservationSystem.Microservices.Bags.Application.GetBagPricing;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;
using ReservationSystem.Microservices.Bags.Models.Mappers;
using ReservationSystem.Microservices.Bags.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Microservices.Bags.Functions;

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
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var pricings = await _getAllHandler.HandleAsync(new GetAllBagPricingsQuery(), cancellationToken);
        var body = new { pricing = BagMapper.ToResponse(pricings) };
        return await req.OkJsonAsync(body);
    }

    [Function("GetBagPricing")]
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
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bag-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateBagPricingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateBagPricingRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateBagPricing request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/bag-pricing/{pricingId:guid}")] HttpRequestData req,
        Guid pricingId,
        CancellationToken cancellationToken)
    {
        UpdateBagPricingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBagPricingRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBagPricing request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
