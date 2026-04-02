using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.OciBags;
using ReservationSystem.Orchestration.Retail.Application.OciPassengerDetails;
using ReservationSystem.Orchestration.Retail.Application.OciRetrieve;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for the Online Check-In (OCI) journey.
/// All OCI endpoints live under /v1/orders/oci/...
/// </summary>
public sealed class OciFunction
{
    private readonly OciRetrieveHandler _ociRetrieveHandler;
    private readonly OciBagsHandler _ociBagsHandler;
    private readonly OciPassengerDetailsHandler _ociPassengerDetailsHandler;
    private readonly ILogger<OciFunction> _logger;

    public OciFunction(
        OciRetrieveHandler ociRetrieveHandler,
        OciBagsHandler ociBagsHandler,
        OciPassengerDetailsHandler ociPassengerDetailsHandler,
        ILogger<OciFunction> logger)
    {
        _ociRetrieveHandler = ociRetrieveHandler;
        _ociBagsHandler = ociBagsHandler;
        _ociPassengerDetailsHandler = ociPassengerDetailsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/oci/retrieve
    // -------------------------------------------------------------------------

    [Function("OciRetrieveOrder")]
    [OpenApiOperation(operationId: "OciRetrieveOrder", tags: new[] { "OCI" }, Summary = "Retrieve an order for online check-in by booking reference and surname")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RetrieveOrderRequest), Required = true, Description = "The OCI retrieval request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OciOrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — bookingReference or surname missing")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — booking reference or surname does not match")]
    public async Task<HttpResponseData> RetrieveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/oci/retrieve")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<RetrieveOrderRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(body!.BookingReference) || string.IsNullOrWhiteSpace(body.Surname))
            return await req.BadRequestAsync("'bookingReference' and 'surname' are required.");

        var result = await _ociRetrieveHandler.HandleAsync(
            new OciRetrieveQuery(
                body.BookingReference.ToUpperInvariant().Trim(),
                body.Surname.Trim()),
            cancellationToken);

        if (result is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/oci/{bookingRef}/passenger-details
    // -------------------------------------------------------------------------

    [Function("OciSavePassengerDetails")]
    [OpenApiOperation(operationId: "OciSavePassengerDetails", tags: new[] { "OCI" }, Summary = "Save passenger APIS travel document data to the booking")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(OciPassengerDetailsRequest), Required = true, Description = "Passenger details including travel document data")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Saved")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SavePassengerDetails(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/oci/{bookingRef}/passenger-details")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<OciPassengerDetailsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (body!.Passengers.Count == 0)
            return await req.BadRequestAsync("'passengers' must not be empty.");

        var command = new OciPassengerDetailsCommand(
            BookingReference: bookingRef.ToUpperInvariant().Trim(),
            Passengers: body.Passengers
                .Select(p => new OciPassengerDetailItem(
                    p.PassengerId,
                    p.TravelDocument is null ? null : new OciTravelDocumentItem(
                        p.TravelDocument.Type,
                        p.TravelDocument.Number,
                        p.TravelDocument.IssuingCountry,
                        p.TravelDocument.Nationality,
                        p.TravelDocument.IssueDate,
                        p.TravelDocument.ExpiryDate)))
                .ToList());

        try
        {
            await _ociPassengerDetailsHandler.HandleAsync(command, cancellationToken);
            return req.NoContent();
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/oci/{bookingRef}/bags
    // -------------------------------------------------------------------------

    [Function("OciAddBags")]
    [OpenApiOperation(operationId: "OciAddBags", tags: new[] { "OCI" }, Summary = "Purchase additional bags during online check-in")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(OciBagsRequest), Required = true, Description = "Bag selections with optional payment details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OciBagsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/oci/{bookingRef}/bags")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<OciBagsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (body!.BagSelections.Count == 0)
            return await req.BadRequestAsync("'bagSelections' must not be empty.");

        var command = new OciBagsCommand(
            BookingReference: bookingRef.ToUpperInvariant().Trim(),
            BagSelections: body.BagSelections
                .Select(b => new OciBagItemCommand(b.PassengerId, b.SegmentRef, b.BagOfferId, b.AdditionalBags))
                .ToList(),
            Payment: body.Payment is null ? null : new OciPaymentCommand(
                body.Payment.Method,
                body.Payment.CardNumber,
                body.Payment.ExpiryDate,
                body.Payment.Cvv,
                body.Payment.CardholderName));

        try
        {
            var result = await _ociBagsHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.BadRequestAsync(ex.Message); }
    }
}
