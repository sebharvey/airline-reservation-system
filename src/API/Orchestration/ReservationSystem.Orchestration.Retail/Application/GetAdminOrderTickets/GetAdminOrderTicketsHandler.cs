using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrderTickets;

public sealed class GetAdminOrderTicketsHandler
{
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public GetAdminOrderTicketsHandler(DeliveryServiceClient deliveryServiceClient)
    {
        _deliveryServiceClient = deliveryServiceClient;
    }

    public async Task<List<AdminTicketRecord>> HandleAsync(string bookingReference, CancellationToken cancellationToken)
    {
        return await _deliveryServiceClient.GetTicketsByBookingAsync(bookingReference, cancellationToken);
    }
}
