using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Ndc;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// IATA NDC 21.3 channel endpoints.
///
/// POST /v1/ndc/AirShopping  — search available flights and offers.
/// POST /v1/ndc/OfferPrice   — validate and re-price a stored offer.
///
/// All routes accept and return application/xml using the IATA NDC 21.3 schema.
/// Errors are returned as application/xml with an NDC Errors element.
/// </summary>
public sealed class NdcFunction
{
    private readonly NdcAirShoppingHandler _airShoppingHandler;
    private readonly NdcOfferPriceHandler _offerPriceHandler;
    private readonly ILogger<NdcFunction> _logger;

    public NdcFunction(
        NdcAirShoppingHandler airShoppingHandler,
        NdcOfferPriceHandler offerPriceHandler,
        ILogger<NdcFunction> logger)
    {
        _airShoppingHandler = airShoppingHandler;
        _offerPriceHandler = offerPriceHandler;
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
            searchResult = await _airShoppingHandler.HandleAsync(command, cancellationToken);
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

    // ── OfferPrice ────────────────────────────────────────────────────────────

    [Function("NdcOfferPrice")]
    public async Task<HttpResponseData> OfferPrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ndc/OfferPrice")] HttpRequestData req,
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
            _logger.LogError(ex, "[NDC] Failed to read OfferPriceRQ body");
            return await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_READ", "Unable to read request body.");
        }

        if (string.IsNullOrWhiteSpace(requestBody))
            return await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_EMPTY_BODY", "Request body must not be empty.");

        // ── Parse OfferPriceRQ ────────────────────────────────────────────────
        var command = NdcXmlParser.TryParseOfferPriceRq(requestBody, out var parseError);
        if (command is null)
        {
            _logger.LogWarning("[NDC] OfferPriceRQ parse failed: {Error}", parseError);
            return await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.BadRequest,
                "ERR_PARSE", parseError ?? "Failed to parse OfferPriceRQ.");
        }

        _logger.LogInformation("[NDC] OfferPrice: OfferRefID={OfferRefId}", command.OfferRefId);

        // ── Reprice offer ─────────────────────────────────────────────────────
        NdcOfferPriceResult result;
        try
        {
            result = await _offerPriceHandler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] OfferPrice failed for {OfferRefId}", command.OfferRefId);
            return await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.InternalServerError,
                "ERR_REPRICE", "An error occurred while pricing the offer.");
        }

        return result.Outcome switch
        {
            NdcOfferPriceOutcome.NotFound =>
                await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.NotFound,
                    "ERR_OFFER_NOT_FOUND", $"Offer {command.OfferRefId} was not found."),

            NdcOfferPriceOutcome.Expired =>
                await XmlOfferPriceErrorResponseAsync(req, HttpStatusCode.Gone,
                    "ERR_OFFER_EXPIRED",
                    "The selected offer has expired or is no longer available. Please search again."),

            _ => await BuildOfferPriceResponseAsync(req, result, command)
        };
    }

    private static async Task<HttpResponseData> BuildOfferPriceResponseAsync(
        HttpRequestData req,
        NdcOfferPriceResult result,
        NdcOfferPriceCommand command)
    {
        var responseXml = NdcXmlBuilder.BuildOfferPriceRS(
            result.OfferDetail!,
            result.RepriceResult!,
            command.Passengers);

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

    private static async Task<HttpResponseData> XmlOfferPriceErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode status,
        string code,
        string message)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OfferPriceRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRS">
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
            </IATA_OfferPriceRS>
            """;

        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(xml, Encoding.UTF8);
        return response;
    }
}
