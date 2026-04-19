using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDocuments;

public sealed class GetAdminOrderDocumentsHandler
{
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public GetAdminOrderDocumentsHandler(DeliveryServiceClient deliveryServiceClient)
    {
        _deliveryServiceClient = deliveryServiceClient;
    }

    public async Task<List<AdminDocumentRecord>> HandleAsync(string bookingReference, CancellationToken cancellationToken)
    {
        return await _deliveryServiceClient.GetDocumentsByBookingAsync(bookingReference, cancellationToken);
    }
}
