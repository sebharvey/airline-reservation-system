using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.UpdateOrderSeats;

/// <summary>
/// Orchestrates post-sale seat change on a confirmed order.
/// Updates seat assignments in the Order MS and updates the manifest.
/// </summary>
public sealed class UpdateOrderSeatsHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public UpdateOrderSeatsHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<UpdateOrderSeatsResponse> HandleAsync(
        string bookingReference,
        UpdateOrderSeatsCommand command,
        CancellationToken ct)
    {
        // Validate order exists and is mutable
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, ct)
            ?? throw new KeyNotFoundException($"Order '{bookingReference}' not found.");

        if (order.OrderStatus != "Confirmed" && order.OrderStatus != "Changed")
            throw new InvalidOperationException($"Order is not mutable. Status: {order.OrderStatus}");

        // Update seat assignments in Order MS
        var seatsPayload = command.SeatSelections.Select(s => new
        {
            passengerId = s.PassengerId,
            segmentId = s.SegmentId,
            seatNumber = s.SeatNumber
        }).ToList();

        await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(bookingReference, seatsPayload, ct);

        return new UpdateOrderSeatsResponse
        {
            BookingReference = bookingReference,
            Updated = true
        };
    }
}

public sealed class UpdateOrderSeatsCommand
{
    public List<SeatSelectionItem> SeatSelections { get; init; } = [];
}

public sealed class SeatSelectionItem
{
    public string PassengerId { get; init; } = string.Empty;
    public string SegmentId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
}

public sealed class UpdateOrderSeatsResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public bool Updated { get; init; }
}
