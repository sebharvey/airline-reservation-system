using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Delivery.Application.CreateDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocument;
using ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;
using ReservationSystem.Microservices.Delivery.Application.VoidDocument;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Web;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class DocumentFunction
{
    private readonly CreateDocumentHandler _createHandler;
    private readonly GetDocumentHandler _getHandler;
    private readonly GetDocumentsByBookingHandler _getByBookingHandler;
    private readonly VoidDocumentHandler _voidHandler;
    private readonly ILogger<DocumentFunction> _logger;

    public DocumentFunction(
        CreateDocumentHandler createHandler,
        GetDocumentHandler getHandler,
        GetDocumentsByBookingHandler getByBookingHandler,
        VoidDocumentHandler voidHandler,
        ILogger<DocumentFunction> logger)
    {
        _createHandler = createHandler;
        _getHandler = getHandler;
        _getByBookingHandler = getByBookingHandler;
        _voidHandler = voidHandler;
        _logger = logger;
    }

    // POST /v1/documents
    [Function("CreateDocument")]
    [OpenApiOperation(operationId: "CreateDocument", tags: new[] { "Documents" }, Summary = "Create a travel document")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateDocumentRequest), Required = true, Description = "Document creation request: documentType, bookingReference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateDocumentResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/documents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateDocumentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

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
    [OpenApiOperation(operationId: "GetDocument", tags: new[] { "Documents" }, Summary = "Get a travel document by ID")]
    [OpenApiParameter(name: "documentId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Document ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetDocumentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
    [OpenApiOperation(operationId: "GetDocumentsByBooking", tags: new[] { "Documents" }, Summary = "Get all documents for a booking")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetDocumentResponse[]), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
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

    // PATCH /v1/documents/{documentNumber}/void
    [Function("VoidDocument")]
    [OpenApiOperation(operationId: "VoidDocument", tags: new[] { "Documents" }, Summary = "Void a travel document")]
    [OpenApiParameter(name: "documentNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Document number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VoidDocumentRequest), Required = true, Description = "Void request: reason, actor")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(VoidDocumentResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> VoidDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/documents/{documentNumber}/void")] HttpRequestData req,
        string documentNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<VoidDocumentRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.Reason))
            return await req.BadRequestAsync("The 'reason' field is required.");

        try
        {
            var command = new VoidDocumentCommand
            {
                DocumentNumber = documentNumber,
                Reason = request.Reason,
                Actor = request.Actor
            };

            var result = await _voidHandler.HandleAsync(command, cancellationToken);
            if (result is null)
                return await req.NotFoundAsync($"Document '{documentNumber}' not found.");
            return await req.OkJsonAsync(result);
        }
        catch (InvalidOperationException ex)
        {
            return await req.UnprocessableEntityAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to void document {DocumentNumber}", documentNumber);
            return await req.InternalServerErrorAsync();
        }
    }
}
