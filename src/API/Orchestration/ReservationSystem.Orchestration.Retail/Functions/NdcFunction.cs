using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderRetrieve;
using ReservationSystem.Orchestration.Retail.Application.NdcServiceList;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Ndc;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// IATA NDC 21.3 channel endpoints.
///
/// POST /v1/ndc/AirShopping    — search available flights and offers.
/// POST /v1/ndc/OfferPrice     — validate and re-price a stored offer.
/// POST /v1/ndc/ServiceList    — retrieve available SSR services (ancillaries).
/// POST /v1/ndc/OrderCreate    — create a confirmed order from a stored offer.
/// POST /v1/ndc/OrderRetrieve  — retrieve a confirmed order by booking reference and surname.
///
/// All routes accept and return application/xml using the IATA NDC 21.3 schema.
/// Errors are returned as application/xml with an NDC Errors element.
/// </summary>
public sealed class NdcFunction
{
    private readonly NdcAirShoppingHandler _airShoppingHandler;
    private readonly NdcOfferPriceHandler _offerPriceHandler;
    private readonly NdcServiceListHandler _serviceListHandler;
    private readonly NdcOrderCreateHandler _orderCreateHandler;
    private readonly NdcOrderRetrieveHandler _orderRetrieveHandler;
    private readonly ILogger<NdcFunction> _logger;

    public NdcFunction(
        NdcAirShoppingHandler airShoppingHandler,
        NdcOfferPriceHandler offerPriceHandler,
        NdcServiceListHandler serviceListHandler,
        NdcOrderCreateHandler orderCreateHandler,
        NdcOrderRetrieveHandler orderRetrieveHandler,
        ILogger<NdcFunction> logger)
    {
        _airShoppingHandler = airShoppingHandler;
        _offerPriceHandler = offerPriceHandler;
        _serviceListHandler = serviceListHandler;
        _orderCreateHandler = orderCreateHandler;
        _orderRetrieveHandler = orderRetrieveHandler;
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

    // ── ServiceList ───────────────────────────────────────────────────────────

    [Function("NdcServiceList")]
    public async Task<HttpResponseData> ServiceList(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ndc/ServiceList")] HttpRequestData req,
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
            _logger.LogError(ex, "[NDC] Failed to read ServiceListRQ body");
            return await XmlServiceListErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_READ", "Unable to read request body.");
        }

        if (string.IsNullOrWhiteSpace(requestBody))
            return await XmlServiceListErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_EMPTY_BODY", "Request body must not be empty.");

        // ── Parse ServiceListRQ ───────────────────────────────────────────────
        var command = NdcXmlParser.TryParseServiceListRq(requestBody, out var parseError);
        if (parseError is not null)
        {
            _logger.LogWarning("[NDC] ServiceListRQ parse failed: {Error}", parseError);
            return await XmlServiceListErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_PARSE", parseError);
        }

        _logger.LogInformation(
            "[NDC] ServiceList: OfferRefId={OfferRefId} CabinCode={CabinCode}",
            command!.OfferRefId, command.NdcCabinCode);

        // ── Fetch services ────────────────────────────────────────────────────
        NdcServiceListResult result;
        try
        {
            result = await _serviceListHandler.HandleAsync(command!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] ServiceList failed");
            return await XmlServiceListErrorAsync(req, HttpStatusCode.InternalServerError,
                "ERR_SERVICE_LIST", "An error occurred while retrieving services.");
        }

        var responseXml = NdcXmlBuilder.BuildServiceListRS(result);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(responseXml, Encoding.UTF8);
        return response;
    }

    private static async Task<HttpResponseData> XmlServiceListErrorAsync(
        HttpRequestData req,
        HttpStatusCode status,
        string code,
        string message)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_ServiceListRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRS">
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
            </IATA_ServiceListRS>
            """;

        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(xml, Encoding.UTF8);
        return response;
    }

    // ── OrderCreate ───────────────────────────────────────────────────────────

    [Function("NdcOrderCreate")]
    public async Task<HttpResponseData> OrderCreate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ndc/OrderCreate")] HttpRequestData req,
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
            _logger.LogError(ex, "[NDC] Failed to read OrderCreateRQ body");
            return await XmlOrderCreateErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_READ", "Unable to read request body.");
        }

        if (string.IsNullOrWhiteSpace(requestBody))
            return await XmlOrderCreateErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_EMPTY_BODY", "Request body must not be empty.");

        // ── Parse OrderCreateRQ ───────────────────────────────────────────────
        var command = NdcXmlParser.TryParseOrderCreateRq(requestBody, out var parseError);
        if (command is null)
        {
            _logger.LogWarning("[NDC] OrderCreateRQ parse failed: {Error}", parseError);
            return await XmlOrderCreateErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_PARSE", parseError ?? "Failed to parse OrderCreateRQ.");
        }

        _logger.LogInformation(
            "[NDC] OrderCreate: OfferRefId={OfferRefId} Pax={PaxCount}",
            command.OfferRefId, command.Passengers.Count);

        // ── Create order ──────────────────────────────────────────────────────
        NdcOrderCreateResult result;
        try
        {
            result = await _orderCreateHandler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] OrderCreate failed for offer {OfferRefId}", command.OfferRefId);
            return await XmlOrderCreateErrorAsync(req, HttpStatusCode.InternalServerError,
                "ERR_ORDER_CREATE", "An error occurred while creating the order.");
        }

        return result.Outcome switch
        {
            NdcOrderCreateOutcome.OfferNotFound =>
                await XmlOrderCreateErrorAsync(req, HttpStatusCode.NotFound,
                    "ERR_OFFER_NOT_FOUND", $"Offer {command.OfferRefId} was not found."),

            NdcOrderCreateOutcome.OfferExpired =>
                await XmlOrderCreateErrorAsync(req, HttpStatusCode.Gone,
                    "ERR_OFFER_EXPIRED",
                    "The selected offer has expired or is no longer available. Please search again."),

            _ => await BuildOrderCreateResponseAsync(req, result, command)
        };
    }

    private static async Task<HttpResponseData> BuildOrderCreateResponseAsync(
        HttpRequestData req,
        NdcOrderCreateResult result,
        NdcOrderCreateCommand command)
    {
        var responseXml = NdcXmlBuilder.BuildOrderCreateRS(result.Order!, command.Passengers);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(responseXml, Encoding.UTF8);
        return response;
    }

    private static async Task<HttpResponseData> XmlOrderCreateErrorAsync(
        HttpRequestData req,
        HttpStatusCode status,
        string code,
        string message)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRS">
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
            </IATA_OrderCreateRS>
            """;

        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(xml, Encoding.UTF8);
        return response;
    }

    // ── OrderRetrieve ─────────────────────────────────────────────────────────

    [Function("NdcOrderRetrieve")]
    public async Task<HttpResponseData> OrderRetrieve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ndc/OrderRetrieve")] HttpRequestData req,
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
            _logger.LogError(ex, "[NDC] Failed to read OrderRetrieveRQ body");
            return await XmlOrderRetrieveErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_READ", "Unable to read request body.");
        }

        if (string.IsNullOrWhiteSpace(requestBody))
            return await XmlOrderRetrieveErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_EMPTY_BODY", "Request body must not be empty.");

        // ── Parse OrderRetrieveRQ ─────────────────────────────────────────────
        var command = NdcXmlParser.TryParseOrderRetrieveRq(requestBody, out var parseError);
        if (command is null)
        {
            _logger.LogWarning("[NDC] OrderRetrieveRQ parse failed: {Error}", parseError);
            return await XmlOrderRetrieveErrorAsync(req, HttpStatusCode.BadRequest,
                "ERR_PARSE", parseError ?? "Failed to parse OrderRetrieveRQ.");
        }

        _logger.LogInformation(
            "[NDC] OrderRetrieve: BookingReference={BookingReference}",
            command.BookingReference);

        // ── Retrieve order ────────────────────────────────────────────────────
        NdcOrderRetrieveResult result;
        try
        {
            result = await _orderRetrieveHandler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NDC] OrderRetrieve failed for {BookingReference}", command.BookingReference);
            return await XmlOrderRetrieveErrorAsync(req, HttpStatusCode.InternalServerError,
                "ERR_ORDER_RETRIEVE", "An error occurred while retrieving the order.");
        }

        if (result.Outcome == NdcOrderRetrieveOutcome.NotFound)
            return await XmlOrderRetrieveErrorAsync(req, HttpStatusCode.NotFound,
                "ERR_ORDER_NOT_FOUND",
                $"Order with booking reference {command.BookingReference} was not found or the surname does not match.");

        var responseXml = NdcXmlBuilder.BuildOrderRetrieveRS(result.Order!);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(responseXml, Encoding.UTF8);
        return response;
    }

    private static async Task<HttpResponseData> XmlOrderRetrieveErrorAsync(
        HttpRequestData req,
        HttpStatusCode status,
        string code,
        string message)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderRetrieveRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderRetrieveRS">
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
            </IATA_OrderRetrieveRS>
            """;

        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(xml, Encoding.UTF8);
        return response;
    }
}
