using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Ndc;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// IATA NDC 21.3 AirShopping endpoint.
///
/// Accepts an IATA_AirShoppingRQ XML document and returns an IATA_AirShoppingRS XML
/// document. The Retail API parses the NDC request, delegates the flight search to the
/// Offer microservice via the same path as the standard slice search, then serialises
/// the result back to NDC-compliant XML.
///
/// Route:  POST /v1/ndc/AirShopping
/// Accepts: application/xml
/// Returns: application/xml
/// </summary>
public sealed class NdcFunction
{
    private readonly NdcAirShoppingHandler _handler;
    private readonly ILogger<NdcFunction> _logger;

    public NdcFunction(NdcAirShoppingHandler handler, ILogger<NdcFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("NdcAirShopping")]
    public async Task<HttpResponseData> AirShopping(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ndc/AirShopping")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // ── Read body ─────────────────────────────────────────────────────────
        string requestBody;
        try
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            requestBody = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] Failed to read AirShoppingRQ body");
            return await XmlErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_READ", "Unable to read request body.");
        }

        if (string.IsNullOrWhiteSpace(requestBody))
            return await XmlErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_EMPTY_BODY", "Request body must not be empty.");

        // ── Parse AirShoppingRQ ───────────────────────────────────────────────
        var command = NdcXmlParser.TryParse(requestBody, out var parseError);
        if (command is null)
        {
            _logger.LogWarning("[NDC] AirShoppingRQ parse failed: {Error}", parseError);
            return await XmlErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_PARSE", parseError ?? "Failed to parse AirShoppingRQ.");
        }

        _logger.LogInformation(
            "[NDC] AirShopping: {Origin}→{Destination} {Date} Pax={Pax}",
            command.Origin, command.Destination, command.DepartureDate, command.TotalPaxCount);

        // ── Search offers ─────────────────────────────────────────────────────
        OfferSearchResultDto searchResult;
        try
        {
            searchResult = await _handler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] Offer search failed for {Origin}→{Destination}",
                command.Origin, command.Destination);
            return await XmlErrorResponseAsync(req, HttpStatusCode.InternalServerError,
                "ERR_SEARCH", "An error occurred while searching for offers.");
        }

        // ── Build AirShoppingRS ───────────────────────────────────────────────
        var responseId = Guid.NewGuid().ToString();
        var responseXml = NdcXmlBuilder.BuildAirShoppingRS(searchResult, command.Passengers, responseId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(responseXml, Encoding.UTF8);
        return response;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<HttpResponseData> XmlErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode status,
        string code,
        string message)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_AirShoppingRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS">
              <Document>
                <ReferenceVersion>21.3</ReferenceVersion>
              </Document>
              <Errors>
                <Error>
                  <Code>{System.Security.SecurityElement.Escape(code)}</Code>
                  <DescText>{System.Security.SecurityElement.Escape(message)}</DescText>
                  <LangCode>EN</LangCode>
                </Error>
              </Errors>
            </IATA_AirShoppingRS>
            """;

        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(xml, Encoding.UTF8);
        return response;
    }
}
