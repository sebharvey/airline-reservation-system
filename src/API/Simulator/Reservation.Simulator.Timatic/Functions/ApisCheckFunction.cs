// Description: POST /autocheck/v1/apischeck — simulates an IATA Timatic APIS check
// called when a passenger submits check-in data. Always returns ACCEPTED on the happy path.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reservation.Simulator.Timatic.Auth;
using Reservation.Simulator.Timatic.Models;

namespace Reservation.Simulator.Timatic.Functions;

public sealed class ApisCheckFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ApisCheckFunction> _logger;

    public ApisCheckFunction(IConfiguration configuration, ILogger<ApisCheckFunction> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    [Function("ApisCheck")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "autocheck/v1/apischeck")]
        HttpRequestData request,
        CancellationToken ct)
    {
        _logger.LogInformation("Timatic ApisCheck called at {UtcNow:O}", DateTime.UtcNow);

        if (!BearerTokenValidator.IsAuthorized(request, _configuration))
            return request.CreateResponse(HttpStatusCode.Unauthorized);

        ApisCheckRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ApisCheckRequest>(
                request.Body, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ApisCheck: invalid request body");
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null)
            return request.CreateResponse(HttpStatusCode.BadRequest);

        var now = DateTime.UtcNow;

        var responseBody = new ApisCheckResponse(
            TransactionIdentifier:    body.TransactionIdentifier,
            ApisStatus:               "ACCEPTED",
            CarrierLiabilityConfirmed: true,
            FineRisk:                 "LOW",
            Warnings:                 Array.Empty<object>(),
            AuditRef:                 GenerateAuditRef(now),
            ProcessedAt:              now.ToString("O")
        );

        return await OkJsonAsync(request, responseBody);
    }

    private static string GenerateAuditRef(DateTime utcNow) =>
        $"TMC-{utcNow:yyyy-MM-dd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static async Task<HttpResponseData> OkJsonAsync<T>(HttpRequestData request, T payload)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions));
        return response;
    }
}
