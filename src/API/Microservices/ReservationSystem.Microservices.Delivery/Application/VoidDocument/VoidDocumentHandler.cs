using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.VoidDocument;

public sealed class VoidDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<VoidDocumentHandler> _logger;

    public VoidDocumentHandler(IDocumentRepository documentRepository, ILogger<VoidDocumentHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public Task<VoidDocumentResponse?> HandleAsync(VoidDocumentCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
