using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;

namespace ReservationSystem.Microservices.Delivery.Functions;

/// <summary>
/// HTTP-triggered functions for Document resource management.
/// Documents represent ancillary revenue records (BagAncillary, SeatAncillary, etc.).
/// </summary>
public sealed class DocumentFunction
{
    private readonly CreateDocumentHandler _createDocumentHandler;
    private readonly GetDocumentHandler _getDocumentHandler;
    private readonly GetDocumentsByBookingHandler _getDocumentsByBookingHandler;
    private readonly ILogger<DocumentFunction> _logger;

    public DocumentFunction(
        CreateDocumentHandler createDocumentHandler,
        GetDocumentHandler getDocumentHandler,
        GetDocumentsByBookingHandler getDocumentsByBookingHandler,
        ILogger<DocumentFunction> logger)
    {
        _createDocumentHandler = createDocumentHandler;
        _getDocumentHandler = getDocumentHandler;
        _getDocumentsByBookingHandler = getDocumentsByBookingHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/documents
    // -------------------------------------------------------------------------

    [Function("CreateDocument")]
    public async Task<HttpResponseData> CreateDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/documents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/documents/{documentId}
    // -------------------------------------------------------------------------

    [Function("GetDocument")]
    public async Task<HttpResponseData> GetDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/documents/{documentId:guid}")] HttpRequestData req,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/documents?bookingRef={bookingRef}
    // -------------------------------------------------------------------------

    [Function("GetDocumentsByBooking")]
    public async Task<HttpResponseData> GetDocumentsByBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/documents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
