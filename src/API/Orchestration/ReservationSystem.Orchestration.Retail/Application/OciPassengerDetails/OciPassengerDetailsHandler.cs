using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Application.OciPassengerDetails;

public sealed class OciPassengerDetailsHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OciPassengerDetailsHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task HandleAsync(OciPassengerDetailsCommand command, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            passengers = command.Passengers.Select(p => new
            {
                passengerId = p.PassengerId,
                travelDocument = p.TravelDocument is null ? null : new
                {
                    type = p.TravelDocument.Type,
                    number = p.TravelDocument.Number,
                    issuingCountry = p.TravelDocument.IssuingCountry,
                    nationality = p.TravelDocument.Nationality,
                    issueDate = p.TravelDocument.IssueDate,
                    expiryDate = p.TravelDocument.ExpiryDate
                }
            })
        }, JsonOptions);

        await _orderServiceClient.UpdateOrderPassengersAsync(command.BookingReference, payload, cancellationToken);
    }
}
