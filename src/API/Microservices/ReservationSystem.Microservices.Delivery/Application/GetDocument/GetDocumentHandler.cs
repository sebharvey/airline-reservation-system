using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetDocument;

/// <summary>
/// Handles the <see cref="GetDocumentQuery"/>.
/// Retrieves a document by its identifier.
/// </summary>
public sealed class GetDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<GetDocumentHandler> _logger;

    public GetDocumentHandler(
        IDocumentRepository repository,
        ILogger<GetDocumentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Document?> HandleAsync(
        GetDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
