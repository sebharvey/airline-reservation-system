using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.CreateDocument;

public sealed class CreateDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<CreateDocumentHandler> _logger;

    public CreateDocumentHandler(IDocumentRepository documentRepository, ILogger<CreateDocumentHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<CreateDocumentResponse> HandleAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var documentDataJson = request.DocumentData.HasValue
            ? request.DocumentData.Value.GetRawText()
            : "{}";

        var document = Document.Create(
            request.DocumentType, request.BookingReference,
            request.ETicketNumber, request.PassengerId, request.SegmentRef,
            request.PaymentReference, request.Amount, request.CurrencyCode,
            documentDataJson);

        await _documentRepository.CreateAsync(document, cancellationToken);

        _logger.LogInformation("Created document {DocumentNumber} ({DocumentType}) for {BookingReference}",
            DeliveryMapper.FormatDocumentNumber(document.DocumentNumber), request.DocumentType, request.BookingReference);

        return DeliveryMapper.ToCreateDocumentResponse(document);
    }
}
