using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetDocument;

public sealed class GetDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<GetDocumentHandler> _logger;

    public GetDocumentHandler(IDocumentRepository documentRepository, ILogger<GetDocumentHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<GetDocumentResponse?> HandleAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return null;
        }
        return DeliveryMapper.ToGetDocumentResponse(document);
    }
}
