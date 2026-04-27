using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.NdcOrderRetrieve;

public enum NdcOrderRetrieveOutcome { Success, NotFound }

public sealed record NdcOrderRetrieveResult(
    NdcOrderRetrieveOutcome Outcome,
    ManagedOrderResponse? Order = null);

/// <summary>
/// Handles POST /v1/ndc/OrderRetrieve.
///
/// Delegates to GetOrderHandler.HandleRetrieveAsync which validates the surname
/// against the lead passenger on the booking before returning the order.
/// Returns NotFound when the booking reference is unknown or the surname does not match.
/// </summary>
public sealed class NdcOrderRetrieveHandler
{
    private readonly GetOrderHandler _getOrderHandler;

    public NdcOrderRetrieveHandler(GetOrderHandler getOrderHandler)
    {
        _getOrderHandler = getOrderHandler;
    }

    public async Task<NdcOrderRetrieveResult> HandleAsync(
        NdcOrderRetrieveCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _getOrderHandler.HandleRetrieveAsync(
            command.BookingReference, command.Surname, cancellationToken);

        return order is null
            ? new NdcOrderRetrieveResult(NdcOrderRetrieveOutcome.NotFound)
            : new NdcOrderRetrieveResult(NdcOrderRetrieveOutcome.Success, order);
    }
}
