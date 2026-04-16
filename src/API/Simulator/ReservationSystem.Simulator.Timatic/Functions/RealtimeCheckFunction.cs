// Description: POST /autocheck/v1/realtimecheck — simulates an IATA Timatic realtime check
// called at the boarding gate when an agent scans the passenger's MRZ.
// Parses ICAO TD3 MRZ lines to populate the parsed document in the response.
// Always returns GO on the happy path.

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

public sealed class RealtimeCheckFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<RealtimeCheckFunction> _logger;

    public RealtimeCheckFunction(IConfiguration configuration, ILogger<RealtimeCheckFunction> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    [Function("RealtimeCheck")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "autocheck/v1/realtimecheck")]
        HttpRequestData request,
        CancellationToken ct)
    {
        _logger.LogInformation("Timatic RealtimeCheck called at {UtcNow:O}", DateTime.UtcNow);

        if (!BearerTokenValidator.IsAuthorized(request, _configuration))
            return request.CreateResponse(HttpStatusCode.Unauthorized);

        RealtimeCheckRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RealtimeCheckRequest>(
                request.Body, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "RealtimeCheck: invalid request body");
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (body is null)
            return request.CreateResponse(HttpStatusCode.BadRequest);

        var parsedDocument = ParseMrz(body.MrzData);
        var now = DateTime.UtcNow;

        var responseBody = new RealtimeCheckResponse(
            TransactionIdentifier:    body.TransactionIdentifier,
            Decision:                 "GO",
            ConditionsMet:            true,
            CarrierLiabilityConfirmed: true,
            ParsedDocument:           parsedDocument,
            AuditRef:                 $"TMC-{now:yyyy-MM-dd}-GATE-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            ProcessedAt:              now.ToString("O")
        );

        return await OkJsonAsync(request, responseBody);
    }

    // Parses ICAO 9303 TD3 Machine Readable Passport MRZ lines.
    //
    // Line 1 (44 chars):
    //   [0]       Document type
    //   [1]       Subtype / filler
    //   [2..4]    Issuing state (3 chars)
    //   [5..43]   Name field — SURNAME<<GIVENNAME1<GIVENNAME2
    //
    // Line 2 (44 chars):
    //   [0..8]    Document number (9 chars)
    //   [9]       Check digit
    //   [10..12]  Nationality (3 chars)
    //   [13..18]  Date of birth YYMMDD
    //   [19]      Check digit
    //   [20]      Sex
    //   [21..26]  Expiry date YYMMDD
    //   [27]      Check digit
    //   [28..43]  Optional / composite data
    private static ParsedDocument ParseMrz(MrzData mrz)
    {
        var line1 = (mrz.Line1 ?? string.Empty).ToUpperInvariant().PadRight(44);
        var line2 = (mrz.Line2 ?? string.Empty).ToUpperInvariant().PadRight(44);

        // Name field starts at position 5 in line 1
        var nameField = line1[5..].TrimEnd('<');
        var nameParts = nameField.Split("<<", 2);
        var surname    = nameParts.Length > 0 ? nameParts[0].Replace('<', ' ').Trim() : string.Empty;
        var givenNames = nameParts.Length > 1 ? nameParts[1].Replace('<', ' ').Trim() : string.Empty;

        var documentNumber = line2[..9].TrimEnd('<');
        var nationality    = line2[10..13].TrimEnd('<');
        var dobRaw         = line2[13..19];
        var sexChar        = line2[20];
        var expiryRaw      = line2[21..27];

        return new ParsedDocument(
            Surname:            surname,
            GivenNames:         givenNames,
            Nationality:        nationality,
            DateOfBirth:        MrzDateToIso(dobRaw),
            DocumentExpiryDate: MrzDateToIso(expiryRaw),
            DocumentNumber:     documentNumber
        );
    }

    // Converts a 6-character YYMMDD MRZ date to an ISO 8601 date string (YYYY-MM-DD).
    // Years >= 60 are treated as 1900s; years < 60 are treated as 2000s.
    private static string MrzDateToIso(string yymmdd)
    {
        if (yymmdd.Length != 6 || !int.TryParse(yymmdd[..2], out var yy)
                                || !int.TryParse(yymmdd[2..4], out var mm)
                                || !int.TryParse(yymmdd[4..6], out var dd))
            return string.Empty;

        var year = yy >= 60 ? 1900 + yy : 2000 + yy;
        return $"{year:D4}-{mm:D2}-{dd:D2}";
    }

    private static async Task<HttpResponseData> OkJsonAsync<T>(HttpRequestData request, T payload)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions));
        return response;
    }
}
