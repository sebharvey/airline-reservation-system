using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.CheckIn;

/// <summary>
/// Persists pre-built order notes to the Order MS, absorbing failures so that a note-writing
/// error never rolls back a successful check-in operation.
/// </summary>
public sealed class CheckInNoteService
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<CheckInNoteService> _logger;

    public CheckInNoteService(
        OrderServiceClient orderServiceClient,
        ILogger<CheckInNoteService> logger)
    {
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    public async Task SaveAsync(
        string bookingReference,
        List<OrderTimaticNote> notes,
        string logContext,
        CancellationToken ct)
    {
        if (notes.Count == 0) return;
        try
        {
            await _orderServiceClient.AddOrderNotesAsync(bookingReference, notes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Context}: failed to write notes to order {BookingReference}",
                logContext, bookingReference);
        }
    }
}
