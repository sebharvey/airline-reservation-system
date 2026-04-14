using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.CreateFareRule;
using ReservationSystem.Microservices.Offer.Application.UpdateFareRule;
using ReservationSystem.Microservices.Offer.Application.DeleteFareRule;
using ReservationSystem.Microservices.Offer.Application.GetFareRule;
using ReservationSystem.Microservices.Offer.Application.SearchFareRules;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Offer.Models.Mappers;
using ReservationSystem.Microservices.Offer.Models.Requests;
using ReservationSystem.Microservices.Offer.Models.Responses;

namespace ReservationSystem.Microservices.Offer.Functions;

public sealed class FareRuleFunction
{
    private readonly CreateFareRuleHandler _createHandler;
    private readonly UpdateFareRuleHandler _updateHandler;
    private readonly DeleteFareRuleHandler _deleteHandler;
    private readonly GetFareRuleHandler _getHandler;
    private readonly SearchFareRulesHandler _searchHandler;
    private readonly ILogger<FareRuleFunction> _logger;

    public FareRuleFunction(
        CreateFareRuleHandler createHandler,
        UpdateFareRuleHandler updateHandler,
        DeleteFareRuleHandler deleteHandler,
        GetFareRuleHandler getHandler,
        SearchFareRulesHandler searchHandler,
        ILogger<FareRuleFunction> logger)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _getHandler = getHandler;
        _searchHandler = searchHandler;
        _logger = logger;
    }

    // POST /v1/fare-rules/search
    [Function("SearchFareRules")]
    [OpenApiOperation(operationId: "SearchFareRules", tags: new[] { "Fare Rules" }, Summary = "Search fare rules")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchFareRulesRequest), Required = false, Description = "Optional search query")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FareRuleResponse>), Description = "OK")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/fare-rules/search")] HttpRequestData req,
        CancellationToken ct)
    {
        string? query = null;
        try
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct);
            if (body.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String)
                query = q.GetString();
        }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in search request body"); }

        var rules = await _searchHandler.HandleAsync(new SearchFareRulesQuery(query), ct);
        return await req.OkJsonAsync(FareRuleMapper.ToResponseList(rules));
    }

    // GET /v1/fare-rules/{fareRuleId}
    [Function("GetFareRule")]
    [MicroserviceCache(1)]
    [OpenApiOperation(operationId: "GetFareRule", tags: new[] { "Fare Rules" }, Summary = "Get a fare rule by ID")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Fare rule ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareRuleResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId, CancellationToken ct)
    {
        var rule = await _getHandler.HandleAsync(new GetFareRuleQuery(fareRuleId), ct);

        if (rule is null)
            return await req.NotFoundAsync($"FareRule '{fareRuleId}' not found.");

        return await req.OkJsonAsync(FareRuleMapper.ToResponse(rule));
    }

    // POST /v1/fare-rules
    [Function("CreateFareRule")]
    [OpenApiOperation(operationId: "CreateFareRule", tags: new[] { "Fare Rules" }, Summary = "Create a new fare rule")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateFareRuleRequest), Required = true, Description = "Fare rule details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(FareRuleResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/fare-rules")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CreateFareRuleCommand(
            RuleType: body.TryGetProperty("ruleType", out var rt) && rt.ValueKind != JsonValueKind.Null ? rt.GetString()! : "Money",
            FlightNumber: body.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetString() : null,
            FareBasisCode: body.GetProperty("fareBasisCode").GetString()!,
            FareFamily: body.TryGetProperty("fareFamily", out var ff) && ff.ValueKind != JsonValueKind.Null ? ff.GetString() : null,
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            BookingClass: body.GetProperty("bookingClass").GetString()!,
            CurrencyCode: body.TryGetProperty("currencyCode", out var cc) && cc.ValueKind != JsonValueKind.Null ? cc.GetString() : null,
            MinAmount: body.TryGetProperty("minAmount", out var mna) && mna.ValueKind != JsonValueKind.Null ? mna.GetDecimal() : null,
            MaxAmount: body.TryGetProperty("maxAmount", out var mxa) && mxa.ValueKind != JsonValueKind.Null ? mxa.GetDecimal() : null,
            MinPoints: body.TryGetProperty("minPoints", out var mnp) && mnp.ValueKind != JsonValueKind.Null ? mnp.GetInt32() : null,
            MaxPoints: body.TryGetProperty("maxPoints", out var mxp) && mxp.ValueKind != JsonValueKind.Null ? mxp.GetInt32() : null,
            PointsTaxes: body.TryGetProperty("pointsTaxes", out var pt) && pt.ValueKind != JsonValueKind.Null ? pt.GetDecimal() : null,
            TaxLines: body.TryGetProperty("taxLines", out var tl) && tl.ValueKind == JsonValueKind.Array ? tl.GetRawText() : null,
            IsRefundable: body.GetProperty("isRefundable").GetBoolean(),
            IsChangeable: body.GetProperty("isChangeable").GetBoolean(),
            ChangeFeeAmount: body.GetProperty("changeFeeAmount").GetDecimal(),
            CancellationFeeAmount: body.GetProperty("cancellationFeeAmount").GetDecimal(),
            ValidFrom: body.TryGetProperty("validFrom", out var vf) && vf.ValueKind != JsonValueKind.Null ? vf.GetString() : null,
            ValidTo: body.TryGetProperty("validTo", out var vt) && vt.ValueKind != JsonValueKind.Null ? vt.GetString() : null);

        try
        {
            var rule = await _createHandler.HandleAsync(command, ct);
            return await req.CreatedAsync($"/v1/fare-rules/{rule.FareRuleId}", FareRuleMapper.ToResponse(rule));
        }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // PUT /v1/fare-rules/{fareRuleId}
    [Function("UpdateFareRule")]
    [OpenApiOperation(operationId: "UpdateFareRule", tags: new[] { "Fare Rules" }, Summary = "Update an existing fare rule")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Fare rule ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateFareRuleRequest), Required = true, Description = "Updated fare rule details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FareRuleResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new UpdateFareRuleCommand(
            FareRuleId: fareRuleId,
            RuleType: body.TryGetProperty("ruleType", out var rt) && rt.ValueKind != JsonValueKind.Null ? rt.GetString()! : "Money",
            FlightNumber: body.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetString() : null,
            FareBasisCode: body.GetProperty("fareBasisCode").GetString()!,
            FareFamily: body.TryGetProperty("fareFamily", out var ff) && ff.ValueKind != JsonValueKind.Null ? ff.GetString() : null,
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            BookingClass: body.GetProperty("bookingClass").GetString()!,
            CurrencyCode: body.TryGetProperty("currencyCode", out var cc) && cc.ValueKind != JsonValueKind.Null ? cc.GetString() : null,
            MinAmount: body.TryGetProperty("minAmount", out var mna) && mna.ValueKind != JsonValueKind.Null ? mna.GetDecimal() : null,
            MaxAmount: body.TryGetProperty("maxAmount", out var mxa) && mxa.ValueKind != JsonValueKind.Null ? mxa.GetDecimal() : null,
            MinPoints: body.TryGetProperty("minPoints", out var mnp) && mnp.ValueKind != JsonValueKind.Null ? mnp.GetInt32() : null,
            MaxPoints: body.TryGetProperty("maxPoints", out var mxp) && mxp.ValueKind != JsonValueKind.Null ? mxp.GetInt32() : null,
            PointsTaxes: body.TryGetProperty("pointsTaxes", out var pt) && pt.ValueKind != JsonValueKind.Null ? pt.GetDecimal() : null,
            TaxLines: body.TryGetProperty("taxLines", out var tl) && tl.ValueKind == JsonValueKind.Array ? tl.GetRawText() : null,
            IsRefundable: body.GetProperty("isRefundable").GetBoolean(),
            IsChangeable: body.GetProperty("isChangeable").GetBoolean(),
            ChangeFeeAmount: body.GetProperty("changeFeeAmount").GetDecimal(),
            CancellationFeeAmount: body.GetProperty("cancellationFeeAmount").GetDecimal(),
            ValidFrom: body.TryGetProperty("validFrom", out var vf) && vf.ValueKind != JsonValueKind.Null ? vf.GetString() : null,
            ValidTo: body.TryGetProperty("validTo", out var vt) && vt.ValueKind != JsonValueKind.Null ? vt.GetString() : null);

        try
        {
            var rule = await _updateHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(FareRuleMapper.ToResponse(rule));
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // DELETE /v1/fare-rules/{fareRuleId}
    [Function("DeleteFareRule")]
    [OpenApiOperation(operationId: "DeleteFareRule", tags: new[] { "Fare Rules" }, Summary = "Delete a fare rule")]
    [OpenApiParameter(name: "fareRuleId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Fare rule ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/fare-rules/{fareRuleId:guid}")] HttpRequestData req,
        Guid fareRuleId, CancellationToken ct)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteFareRuleCommand(fareRuleId), ct);

        if (!deleted)
            return await req.NotFoundAsync($"FareRule '{fareRuleId}' not found.");

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

}
