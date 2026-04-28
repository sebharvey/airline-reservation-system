using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.CreateWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.DeleteWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.GetAllWatchlistEntries;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.GetWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.UpdateWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class WatchlistFunction
{
    private readonly GetAllWatchlistEntriesHandler _getAllHandler;
    private readonly GetWatchlistEntryHandler _getHandler;
    private readonly CreateWatchlistEntryHandler _createHandler;
    private readonly UpdateWatchlistEntryHandler _updateHandler;
    private readonly DeleteWatchlistEntryHandler _deleteHandler;
    private readonly ILogger<WatchlistFunction> _logger;

    public WatchlistFunction(
        GetAllWatchlistEntriesHandler getAllHandler,
        GetWatchlistEntryHandler getHandler,
        CreateWatchlistEntryHandler createHandler,
        UpdateWatchlistEntryHandler updateHandler,
        DeleteWatchlistEntryHandler deleteHandler,
        ILogger<WatchlistFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _getHandler = getHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    [Function("GetAllWatchlistEntries")]
    [OpenApiOperation(operationId: "GetAllWatchlistEntries", tags: new[] { "Watchlist" }, Summary = "List all watchlist entries")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<WatchlistEntryResponse>), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/watchlist-entries")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var entries = await _getAllHandler.HandleAsync(new GetAllWatchlistEntriesQuery(), cancellationToken);
        return await req.OkJsonAsync(new { entries = WatchlistMapper.ToResponse(entries) });
    }

    [Function("GetWatchlistEntry")]
    [OpenApiOperation(operationId: "GetWatchlistEntry", tags: new[] { "Watchlist" }, Summary = "Get a watchlist entry by ID")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(WatchlistEntryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        var entry = await _getHandler.HandleAsync(new GetWatchlistEntryQuery(watchlistId), cancellationToken);
        if (entry is null)
            return await req.NotFoundAsync($"No watchlist entry found for ID '{watchlistId}'.");
        return await req.OkJsonAsync(WatchlistMapper.ToResponse(entry));
    }

    [Function("CreateWatchlistEntry")]
    [OpenApiOperation(operationId: "CreateWatchlistEntry", tags: new[] { "Watchlist" }, Summary = "Add a passenger to the watchlist")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateWatchlistEntryRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(WatchlistEntryResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — passport number already on watchlist")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/watchlist-entries")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateWatchlistEntryRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.GivenName))
            return await req.BadRequestAsync("The 'givenName' field is required.");
        if (string.IsNullOrWhiteSpace(request.Surname))
            return await req.BadRequestAsync("The 'surname' field is required.");
        if (string.IsNullOrWhiteSpace(request.DateOfBirth) || !DateOnly.TryParse(request.DateOfBirth, out _))
            return await req.BadRequestAsync("The 'dateOfBirth' field is required in yyyy-MM-dd format.");
        if (string.IsNullOrWhiteSpace(request.PassportNumber))
            return await req.BadRequestAsync("The 'passportNumber' field is required.");

        try
        {
            var command = WatchlistMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/watchlist-entries/{created.WatchlistId}", WatchlistMapper.ToResponse(created));
        }
        catch (Exception ex) when (ex.Message.Contains("UQ_Watchlist_Passport") || ex.Message.Contains("UNIQUE") || ex.InnerException?.Message.Contains("UQ_Watchlist_Passport") == true)
        {
            return await req.ConflictAsync($"Passport number '{request.PassportNumber}' is already on the watchlist.");
        }
    }

    [Function("UpdateWatchlistEntry")]
    [OpenApiOperation(operationId: "UpdateWatchlistEntry", tags: new[] { "Watchlist" }, Summary = "Update a watchlist entry")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateWatchlistEntryRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(WatchlistEntryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateWatchlistEntryRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.GivenName))
            return await req.BadRequestAsync("The 'givenName' field is required.");
        if (string.IsNullOrWhiteSpace(request.Surname))
            return await req.BadRequestAsync("The 'surname' field is required.");
        if (string.IsNullOrWhiteSpace(request.DateOfBirth) || !DateOnly.TryParse(request.DateOfBirth, out _))
            return await req.BadRequestAsync("The 'dateOfBirth' field is required in yyyy-MM-dd format.");
        if (string.IsNullOrWhiteSpace(request.PassportNumber))
            return await req.BadRequestAsync("The 'passportNumber' field is required.");

        try
        {
            var command = WatchlistMapper.ToCommand(watchlistId, request);
            var updated = await _updateHandler.HandleAsync(command, cancellationToken);
            if (updated is null)
                return await req.NotFoundAsync($"No watchlist entry found for ID '{watchlistId}'.");
            return await req.OkJsonAsync(WatchlistMapper.ToResponse(updated));
        }
        catch (Exception ex) when (ex.Message.Contains("UQ_Watchlist_Passport") || ex.Message.Contains("UNIQUE") || ex.InnerException?.Message.Contains("UQ_Watchlist_Passport") == true)
        {
            return await req.ConflictAsync($"Passport number '{request.PassportNumber}' is already on the watchlist.");
        }
    }

    [Function("DeleteWatchlistEntry")]
    [OpenApiOperation(operationId: "DeleteWatchlistEntry", tags: new[] { "Watchlist" }, Summary = "Remove a passenger from the watchlist")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteWatchlistEntryCommand(watchlistId), cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No watchlist entry found for ID '{watchlistId}'.");
    }
}
