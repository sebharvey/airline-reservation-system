using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;

/// <summary>
/// Handles the <see cref="GetDocumentsByBookingQuery"/>.
/// Retrieves all documents for a given booking reference.
/// </summary>
public sealed class GetDocumentsByBookingHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<GetDocumentsByBookingHandler> _logger;

    public GetDocumentsByBookingHandler(
        IDocumentRepository repository,
        ILogger<GetDocumentsByBookingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Document>> HandleAsync(
        GetDocumentsByBookingQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
