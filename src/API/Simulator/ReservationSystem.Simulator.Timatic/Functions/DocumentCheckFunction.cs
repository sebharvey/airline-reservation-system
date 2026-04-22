// Description: POST /autocheck/v1/documentcheck — simulates an IATA Timatic document check
// called at booking or OCI entry to verify passport, visa, and health document requirements.
// Always returns a happy-path OK response with no requirements and no visa needed.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservationSystem.Simulator.Timatic.Auth;
using ReservationSystem.Simulator.Timatic.Models;

namespace ReservationSystem.Simulator.Timatic.Functions;

public sealed class DocumentCheckFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy         = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive  = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentCheckFunction> _logger;

    public DocumentCheckFunction(IConfiguration configuration, ILogger<DocumentCheckFunction> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    [Function("DocumentCheck")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "autocheck/v1/documentcheck")]
        HttpRequestData request,
        CancellationToken ct)
    {
        _logger.LogInformation("Timatic DocumentCheck called at {UtcNow:O}", DateTime.UtcNow);

        if (!BearerTokenValidator.IsAuthorized(request, _configuration))
            return request.CreateResponse(HttpStatusCode.Unauthorized);

        DocumentCheckRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<DocumentCheckRequest>(
                request.Body, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "DocumentCheck: invalid request body");
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null)
            return request.CreateResponse(HttpStatusCode.BadRequest);

        // Non-successful state: FAILED — triggered when documentNumber starts with "FAIL" (case-insensitive).
        // Simulates a document check failure where the travel document is not accepted.
        if (body.PaxInfo.DocumentNumber.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            var failedBody = new DocumentCheckResponse(
                TransactionIdentifier: body.TransactionIdentifier,
                Status:                "FAILED",
                PassportRequired:      true,
                VisaRequired:          true,
                HealthDocRequired:     false,
                TransitVisaRequired:   false,
                Requirements: new object[]
                {
                    new DocumentRequirement(
                        Type:        "VISA",
                        Description: "Visa required. Travel document number is not accepted.",
                        Mandatory:   true
                    )
                },
                Advisories: Array.Empty<Advisory>(),
                DataAsOf:   DateTime.UtcNow.Date.AddHours(8).ToString("O")
            );
            return await OkJsonAsync(request, failedBody);
        }

        var advisories = BuildAdvisories(body);

        var responseBody = new DocumentCheckResponse(
            TransactionIdentifier: body.TransactionIdentifier,
            Status:                "OK",
            PassportRequired:      true,
            VisaRequired:          false,
            HealthDocRequired:     false,
            TransitVisaRequired:   false,
            Requirements:          Array.Empty<object>(),
            Advisories:            advisories,
            DataAsOf:              DateTime.UtcNow.Date.AddHours(8).ToString("O")
        );

        return await OkJsonAsync(request, responseBody);
    }

    // Returns destination-specific travel advisories for the first arrival airport.
    // Happy-path only — add ESTA for US destinations, ETA for UK arrivals.
    private static IReadOnlyList<Advisory> BuildAdvisories(DocumentCheckRequest body)
    {
        var advisories = new List<Advisory>();
        var arrival    = body.Itinerary.LastOrDefault()?.ArrivalAirport;

        // US destinations require ESTA for non-US/non-VWP travellers
        if (IsUsAirport(arrival))
        {
            advisories.Add(new Advisory(
                Type:        "ESTA",
                Description: "ESTA registration required prior to travel.",
                Url:         "https://esta.cbp.dhs.gov",
                Mandatory:   true
            ));
        }

        return advisories;
    }

    private static bool IsUsAirport(string? iataCode) =>
        iataCode is "JFK" or "LAX" or "ORD" or "ATL" or "DFW" or "MIA"
            or "SFO" or "SEA" or "BOS" or "EWR" or "IAD" or "IAH"
            or "MCO" or "MSP" or "DTW" or "PHL" or "LGA" or "CLT"
            or "DEN" or "PHX" or "LAS";

    private static async Task<HttpResponseData> OkJsonAsync<T>(HttpRequestData request, T payload)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions));
        return response;
    }
}
