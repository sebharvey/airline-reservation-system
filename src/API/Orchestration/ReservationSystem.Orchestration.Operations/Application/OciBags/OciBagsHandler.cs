using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciBags;

public sealed record OciBagSelection(string PassengerId, int BagCount, string? BagOfferId = null);

public sealed record OciBagsCommand(
    string BookingReference,
    IReadOnlyList<OciBagSelection> Bags);

public sealed record OciBagAssignment(
    string PassengerId,
    int BagCount,
    string? BagOfferId);

public sealed record OciBagsResult(
    string BookingReference,
    IReadOnlyList<OciBagAssignment> Assignments);

public sealed class OciBagsHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly BagServiceClient _bagServiceClient;
    private readonly ILogger<OciBagsHandler> _logger;

    public OciBagsHandler(
        OrderServiceClient orderServiceClient,
        BagServiceClient bagServiceClient,
        ILogger<OciBagsHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _bagServiceClient = bagServiceClient;
        _logger = logger;
    }

    public async Task<OciBagsResult?> HandleAsync(OciBagsCommand command, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);
        if (order is null) return null;

        var currency = order.CurrencyCode;
        var assignments = new List<OciBagAssignment>();
        var bagsPayload = new List<object>();

        foreach (var selection in command.Bags)
        {
            // When a bagOfferId is supplied, validate it against the Ancillary MS
            if (!string.IsNullOrWhiteSpace(selection.BagOfferId))
            {
                var offer = await _bagServiceClient.GetBagOfferAsync(selection.BagOfferId, ct);
                if (offer is null || !offer.IsValid)
                    throw new InvalidOperationException(
                        $"Bag offer '{selection.BagOfferId}' is not valid or has expired.");
            }

            assignments.Add(new OciBagAssignment(
                selection.PassengerId, selection.BagCount, selection.BagOfferId));

            bagsPayload.Add(new
            {
                passengerId    = selection.PassengerId,
                bagOfferId     = selection.BagOfferId,
                additionalBags = selection.BagCount,
                price          = 0m,
                tax            = 0m,
                currency
            });
        }

        if (bagsPayload.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderBagsPostSaleAsync(
                    command.BookingReference, bagsPayload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OCI bags: failed to persist bag selections to order {BookingReference}",
                    command.BookingReference);
            }
        }

        return new OciBagsResult(command.BookingReference, assignments);
    }
}
