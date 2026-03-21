using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.CreateDocument;

/// <summary>
/// Handles the <see cref="CreateDocumentCommand"/>.
/// Creates and persists a new delivery document.
/// </summary>
public sealed class CreateDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<CreateDocumentHandler> _logger;

    public CreateDocumentHandler(
        IDocumentRepository repository,
        ILogger<CreateDocumentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Document> HandleAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
