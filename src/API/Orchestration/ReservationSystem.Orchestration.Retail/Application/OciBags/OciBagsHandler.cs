using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Application.OciBags;

public sealed class OciBagsHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OciBagsHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<OciBagsResponse> HandleAsync(OciBagsCommand command, CancellationToken cancellationToken)
    {
        var totalBags = command.BagSelections.Sum(b => b.AdditionalBags);

        if (totalBags > 0)
        {
            var payload = JsonSerializer.Serialize(new
            {
                bagSelections = command.BagSelections.Select(b => new
                {
                    passengerId = b.PassengerId,
                    segmentRef = b.SegmentRef,
                    bagOfferId = b.BagOfferId,
                    additionalBags = b.AdditionalBags
                })
            }, JsonOptions);

            await _orderServiceClient.UpdateOrderBagsAsync(command.BookingReference, payload, cancellationToken);
        }

        return new OciBagsResponse
        {
            BookingReference = command.BookingReference,
            BagsPurchased = totalBags,
        };
    }
}
