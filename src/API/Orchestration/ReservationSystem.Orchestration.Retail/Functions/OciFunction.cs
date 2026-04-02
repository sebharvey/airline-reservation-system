using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
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
    private readonly ILogger<OciFunction> _logger;

    public OciFunction(OciRetrieveHandler ociRetrieveHandler, ILogger<OciFunction> logger)
    {
        _ociRetrieveHandler = ociRetrieveHandler;
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
}
