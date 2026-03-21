using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;
using System.Web;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class DocumentFunction
{
    private readonly CreateDocumentHandler _createHandler;
    private readonly GetDocumentHandler _getHandler;
    private readonly GetDocumentsByBookingHandler _getByBookingHandler;
    private readonly ILogger<DocumentFunction> _logger;

    public DocumentFunction(
        CreateDocumentHandler createHandler,
        GetDocumentHandler getHandler,
        GetDocumentsByBookingHandler getByBookingHandler,
        ILogger<DocumentFunction> logger)
    {
        _createHandler = createHandler;
        _getHandler = getHandler;
        _getByBookingHandler = getByBookingHandler;
        _logger = logger;
    }

    // POST /v1/documents
    [Function("CreateDocument")]
    public async Task<HttpResponseData> CreateDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/documents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateDocumentRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateDocumentRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateDocument request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.DocumentType))
            return await req.BadRequestAsync("The 'documentType' field is required.");

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        try
        {
            var result = await _createHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync($"/v1/documents/{result.DocumentId}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create document");
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/documents/{documentId}
    [Function("GetDocument")]
    public async Task<HttpResponseData> GetDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/documents/{documentId:guid}")] HttpRequestData req,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _getHandler.HandleAsync(documentId, cancellationToken);
            if (result is null)
                return await req.NotFoundAsync($"Document '{documentId}' not found.");
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId}", documentId);
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/documents?bookingRef={bookingRef}
    [Function("GetDocumentsByBooking")]
    public async Task<HttpResponseData> GetDocumentsByBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/documents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var bookingRef = query["bookingRef"];

        if (string.IsNullOrWhiteSpace(bookingRef))
            return await req.BadRequestAsync("Query parameter 'bookingRef' is required.");

        try
        {
            var result = await _getByBookingHandler.HandleAsync(bookingRef, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get documents for booking {BookingRef}", bookingRef);
            return await req.InternalServerErrorAsync();
        }
    }
}
