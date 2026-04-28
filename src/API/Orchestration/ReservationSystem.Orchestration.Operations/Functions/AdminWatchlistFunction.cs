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
using System.Text.Json;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// Staff-facing departure-control watchlist endpoints. The "Admin" function name prefix
/// activates <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>,
/// requiring a valid staff JWT for all calls.
/// </summary>
public sealed class AdminWatchlistFunction
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<AdminWatchlistFunction> _logger;

    public AdminWatchlistFunction(DeliveryServiceClient deliveryServiceClient, ILogger<AdminWatchlistFunction> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    [Function("AdminGetAllWatchlistEntries")]
    [OpenApiOperation(operationId: "AdminGetAllWatchlistEntries", tags: new[] { "Admin Watchlist" }, Summary = "List all watchlist entries (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<WatchlistEntryDto>), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/watchlist-entries")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _deliveryServiceClient.GetAllWatchlistEntriesAsync(cancellationToken);
            return await req.OkJsonAsync(new { entries });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list watchlist entries");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminGetWatchlistEntry")]
    [OpenApiOperation(operationId: "AdminGetWatchlistEntry", tags: new[] { "Admin Watchlist" }, Summary = "Get a watchlist entry by ID (staff)")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(WatchlistEntryDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _deliveryServiceClient.GetWatchlistEntryAsync(watchlistId, cancellationToken);
            if (entry is null)
                return await req.NotFoundAsync($"Watchlist entry '{watchlistId}' not found.");
            return await req.OkJsonAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get watchlist entry {WatchlistId}", watchlistId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminCreateWatchlistEntry")]
    [OpenApiOperation(operationId: "AdminCreateWatchlistEntry", tags: new[] { "Admin Watchlist" }, Summary = "Add a passenger to the watchlist (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCreateWatchlistEntryRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(WatchlistEntryDto), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — passport already on watchlist")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/watchlist-entries")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCreateWatchlistEntryRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.GivenName))
            return await req.BadRequestAsync("The 'givenName' field is required.");
        if (string.IsNullOrWhiteSpace(request.Surname))
            return await req.BadRequestAsync("The 'surname' field is required.");
        if (string.IsNullOrWhiteSpace(request.DateOfBirth) || !DateOnly.TryParse(request.DateOfBirth, out _))
            return await req.BadRequestAsync("The 'dateOfBirth' field is required in yyyy-MM-dd format.");
        if (string.IsNullOrWhiteSpace(request.PassportNumber))
            return await req.BadRequestAsync("The 'passportNumber' field is required.");

        var addedBy = ExtractUsername(req);

        try
        {
            var payload = new
            {
                givenName = request.GivenName,
                surname = request.Surname,
                dateOfBirth = request.DateOfBirth,
                passportNumber = request.PassportNumber,
                addedBy,
                notes = request.Notes,
            };
            var created = await _deliveryServiceClient.CreateWatchlistEntryAsync(payload, cancellationToken);
            return await req.CreatedAsync($"/v1/admin/watchlist-entries/{created.WatchlistId}", created);
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
            _logger.LogError(ex, "Failed to create watchlist entry");
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminUpdateWatchlistEntry")]
    [OpenApiOperation(operationId: "AdminUpdateWatchlistEntry", tags: new[] { "Admin Watchlist" }, Summary = "Update a watchlist entry (staff)")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateWatchlistEntryRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(WatchlistEntryDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict — passport already on watchlist")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateWatchlistEntryRequest>(_logger, cancellationToken);
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
            var payload = new
            {
                givenName = request.GivenName,
                surname = request.Surname,
                dateOfBirth = request.DateOfBirth,
                passportNumber = request.PassportNumber,
                notes = request.Notes,
            };
            var updated = await _deliveryServiceClient.UpdateWatchlistEntryAsync(watchlistId, payload, cancellationToken);
            return await req.OkJsonAsync(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return await req.NotFoundAsync(ex.Message);
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
            _logger.LogError(ex, "Failed to update watchlist entry {WatchlistId}", watchlistId);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDeleteWatchlistEntry")]
    [OpenApiOperation(operationId: "AdminDeleteWatchlistEntry", tags: new[] { "Admin Watchlist" }, Summary = "Remove a passenger from the watchlist (staff)")]
    [OpenApiParameter(name: "watchlistId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Staff JWT required")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/watchlist-entries/{watchlistId:guid}")] HttpRequestData req,
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _deliveryServiceClient.DeleteWatchlistEntryAsync(watchlistId, cancellationToken);
            if (!found)
                return await req.NotFoundAsync($"Watchlist entry '{watchlistId}' not found.");
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete watchlist entry {WatchlistId}", watchlistId);
            return await req.InternalServerErrorAsync();
        }
    }

    private static string ExtractUsername(HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues("Authorization", out var values))
                return "Staff";

            var bearer = values.FirstOrDefault();
            if (bearer?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) != true)
                return "Staff";

            var token = bearer[7..];
            var parts = token.Split('.');
            if (parts.Length != 3)
                return "Staff";

            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("unique_name", out var uniqueName) && uniqueName.GetString() is { } un && !string.IsNullOrWhiteSpace(un))
                return un;

            if (root.TryGetProperty("sub", out var sub) && sub.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch { }

        return "Staff";
    }
}
