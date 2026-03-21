using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;

public sealed class GetDocumentsByBookingHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<GetDocumentsByBookingHandler> _logger;

    public GetDocumentsByBookingHandler(IDocumentRepository documentRepository, ILogger<GetDocumentsByBookingHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GetDocumentResponse>> HandleAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetByBookingReferenceAsync(bookingReference, cancellationToken);
        return documents.Select(DeliveryMapper.ToGetDocumentResponse).ToList().AsReadOnly();
    }
}
