using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionRebookOrder;

public sealed class AdminDisruptionRebookOrderHandler
{
    private const int AvailabilityLookaheadDays = 7;

    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<AdminDisruptionRebookOrderHandler> _logger;

    public AdminDisruptionRebookOrderHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<AdminDisruptionRebookOrderHandler> logger)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<AdminDisruptionRebookOrderResponse> HandleAsync(
        AdminDisruptionRebookOrderCommand command,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "IROPS single-order rebook started for booking {BookingRef} on flight {FlightNumber}/{DepartureDate}",
                command.BookingReference, command.FlightNumber, command.DepartureDate);

            var flightInventory = await _offerServiceClient.GetFlightInventoryAsync(command.FlightNumber, command.DepartureDate, ct);
            if (flightInventory is null)
                return Failed(command.BookingReference, $"Flight {command.FlightNumber} on {command.DepartureDate} not found.");

            var origin = flightInventory.Origin;
            var destination = flightInventory.Destination;

            // Resolve the order directly from Order MS — does not rely on manifest.
            var allOrders = await _orderServiceClient.GetOrdersByFlightAsync(
                command.FlightNumber, command.DepartureDate, "Confirmed", ct);

            var order = allOrders.Orders.FirstOrDefault(o =>
                string.Equals(o.BookingReference, command.BookingReference, StringComparison.OrdinalIgnoreCase));

            if (order is null)
                return Failed(command.BookingReference,
                    $"Booking {command.BookingReference} not found on flight {command.FlightNumber}/{command.DepartureDate}.");

            // Find the best available replacement flight.
            var availability = await _offerServiceClient.GetFlightAvailabilityAsync(
                origin, destination, command.DepartureDate, AvailabilityLookaheadDays, ct);

            var replacementPool = availability.Flights
                .SelectMany(flight => flight.Cabins
                    .Select(cabin => new RebookReplacementOption
                    {
                        DepartureDate = flight.DepartureDate,
                        DepartureTime = flight.DepartureTime,
                        CabinCode = cabin.CabinCode,
                        Legs =
                        [
                            new RebookReplacementLeg
                            {
                                OfferId = string.Empty,
                                InventoryId = flight.InventoryId,
                                FlightNumber = flight.FlightNumber,
                                DepartureDate = flight.DepartureDate,
                                DepartureTime = flight.DepartureTime,
                                ArrivalTime = flight.ArrivalTime,
                                ArrivalDayOffset = flight.ArrivalDayOffset,
                                Origin = flight.Origin,
                                Destination = flight.Destination,
                                SeatsAvailable = cabin.SeatsAvailable
                            }
                        ]
                    }))
                .ToList();

            var replacement = FindBestReplacement(replacementPool, order.Segment.CabinCode, order.Passengers.Count);

            if (replacement is null)
            {
                _logger.LogWarning(
                    "No replacement found for booking {BookingRef} — no seats on {Origin}→{Destination} within {Days} days",
                    command.BookingReference, origin, destination, AvailabilityLookaheadDays);

                return Failed(command.BookingReference,
                    $"No available flights from {origin} to {destination} within {AvailabilityLookaheadDays} days.");
            }

            return await RebookOrderAsync(order, command.FlightNumber, command.DepartureDate, replacement, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while rebooking {BookingRef} on {FlightNumber}/{DepartureDate}",
                command.BookingReference, command.FlightNumber, command.DepartureDate);

            return Failed(command.BookingReference, ex.Message);
        }
    }

    private async Task<AdminDisruptionRebookOrderResponse> RebookOrderAsync(
        AffectedOrderDto order,
        string cancelledFlightNumber,
        string cancelledDepartureDate,
        RebookReplacementOption replacement,
        CancellationToken ct)
    {
        var passengerIds = order.Passengers.Select(p => p.PassengerId).ToList();
        var heldLegs = new List<RebookReplacementLeg>();

        foreach (var leg in replacement.Legs)
        {
            try
            {
                await _offerServiceClient.HoldInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerIds, order.OrderId, ct);
                heldLegs.Add(leg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hold failed on {FlightNumber}/{Date} for booking {BookingRef} — releasing {Count} held leg(s)",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference, heldLegs.Count);

                await ReleaseHeldLegsAsync(heldLegs, replacement.CabinCode, passengerIds.Count, order);

                return Failed(order.BookingReference,
                    $"Inventory hold failed on {leg.FlightNumber}/{leg.DepartureDate}: {ex.Message}");
            }
        }

        var rebookRequest = new RebookOrderRequest
        {
            CancelledSegmentId = order.Segment.SegmentId,
            ReplacementOfferIds = replacement.Legs.Select(l => l.OfferId).ToList(),
            Reason = "FlightCancellation",
            BookingType = order.BookingType,
            FromFlightNumber = cancelledFlightNumber,
            FromDepartureDate = cancelledDepartureDate,
            ToFlights = replacement.Legs.Select(l => new RebookToFlightDto
            {
                FlightNumber = l.FlightNumber,
                DepartureDate = l.DepartureDate,
                InventoryId = l.InventoryId.ToString()
            }).ToList()
        };

        try
        {
            await _orderServiceClient.RebookOrderAsync(order.BookingReference, rebookRequest, ct);
        }
        catch (Exception ex)
        {
            await ReleaseHeldLegsAsync(heldLegs, replacement.CabinCode, passengerIds.Count, order);
            return Failed(order.BookingReference, $"Order rebook failed: {ex.Message}");
        }

        var toItems = replacement.Legs
            .Select(l => (l.InventoryId, replacement.CabinCode))
            .ToList();

        try
        {
            await _offerServiceClient.RebookInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, toItems, order.OrderId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rebook inventory for booking {BookingRef} — seats may be inconsistent",
                order.BookingReference);
        }

        var existingTickets = await _deliveryServiceClient.GetTicketsByBookingAsync(order.BookingReference, ct);
        var ticketsToVoid = existingTickets.Where(t => !t.IsVoided).Select(t => t.ETicketNumber).ToList();

        var reissueRequest = new ReissueTicketsRequest
        {
            BookingReference = order.BookingReference,
            CancelledETicketNumbers = ticketsToVoid,
            Passengers = order.Passengers.Select(pax => new ReissuePassengerDto
            {
                PassengerId = pax.PassengerId,
                GivenName = pax.GivenName,
                Surname = pax.Surname,
                PassengerTypeCode = pax.PassengerType
            }).ToList(),
            Segments = replacement.Legs.Select(l => new ReissueSegmentDto
            {
                InventoryId = l.InventoryId,
                FlightNumber = l.FlightNumber,
                DepartureDate = l.DepartureDate,
                DepartureTime = l.DepartureTime,
                Origin = l.Origin,
                Destination = l.Destination,
                CabinCode = replacement.CabinCode
            }).ToList(),
            Reason = "FlightCancellation",
            Actor = "OperationsAPI"
        };

        ReissueTicketsResponse reissueResponse;
        try
        {
            reissueResponse = await _deliveryServiceClient.ReissueTicketsAsync(reissueRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reissue tickets for booking {BookingRef}", order.BookingReference);
            // Order is already rebooked — return success but log the ticket reissue failure.
            return new AdminDisruptionRebookOrderResponse
            {
                BookingReference = order.BookingReference,
                Outcome = "Rebooked",
                ReplacementFlightNumber = string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
                ReplacementDepartureDate = replacement.DepartureDate
            };
        }

        foreach (var replacementLeg in replacement.Legs)
        {
            var passengers = order.Passengers.Select(pax =>
            {
                var newTicket = reissueResponse.Tickets.FirstOrDefault(t => t.PassengerId == pax.PassengerId);
                if (newTicket is null)
                    _logger.LogWarning("No reissued ticket for passenger {PassengerId} on {BookingRef} leg {FlightNumber}",
                        pax.PassengerId, order.BookingReference, replacementLeg.FlightNumber);
                return new RebookManifestPassengerDto
                {
                    PassengerId = pax.PassengerId,
                    ETicketNumber = newTicket?.ETicketNumber ?? string.Empty
                };
            }).ToList();

            try
            {
                await _deliveryServiceClient.RebookManifestAsync(
                    order.BookingReference,
                    cancelledFlightNumber,
                    cancelledDepartureDate,
                    new RebookManifestRequest
                    {
                        ToInventoryId = replacementLeg.InventoryId,
                        ToFlightNumber = replacementLeg.FlightNumber,
                        ToOrigin = replacementLeg.Origin,
                        ToDestination = replacementLeg.Destination,
                        ToDepartureDate = replacementLeg.DepartureDate,
                        ToDepartureTime = replacementLeg.DepartureTime,
                        ToArrivalTime = replacementLeg.ArrivalTime,
                        ToCabinCode = replacement.CabinCode,
                        Passengers = passengers
                    },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update manifest for booking {BookingRef} leg {FlightNumber}",
                    order.BookingReference, replacementLeg.FlightNumber);
            }
        }

        _logger.LogInformation(
            "Booking {BookingRef} rebooked: {Cancelled} → {Replacement} on {Date}",
            order.BookingReference, cancelledFlightNumber,
            string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
            replacement.DepartureDate);

        return new AdminDisruptionRebookOrderResponse
        {
            BookingReference = order.BookingReference,
            Outcome = "Rebooked",
            ReplacementFlightNumber = string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
            ReplacementDepartureDate = replacement.DepartureDate
        };
    }

    private async Task ReleaseHeldLegsAsync(
        IReadOnlyList<RebookReplacementLeg> heldLegs,
        string cabinCode,
        int passengerCount,
        AffectedOrderDto order)
    {
        foreach (var held in heldLegs)
        {
            try
            {
                await _offerServiceClient.ReleaseInventoryAsync(
                    held.InventoryId, cabinCode, passengerCount, order.OrderId, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                _logger.LogError(releaseEx, "Failed to release inventory {InventoryId} after failure for {BookingRef}",
                    held.InventoryId, order.BookingReference);
            }
        }
    }

    private static AdminDisruptionRebookOrderResponse Failed(string bookingReference, string reason) =>
        new() { BookingReference = bookingReference, Outcome = "Failed", FailureReason = reason };

    private static RebookReplacementOption? FindBestReplacement(
        List<RebookReplacementOption> options,
        string originalCabinCode,
        int seatsNeeded)
    {
        foreach (var cabinCode in GetCabinSearchOrder(originalCabinCode))
        {
            var candidate = options.FirstOrDefault(o =>
                o.CabinCode == cabinCode &&
                o.Legs.All(l => l.SeatsAvailable >= seatsNeeded));

            if (candidate is not null)
                return candidate;
        }
        return null;
    }

    private static IEnumerable<string> GetCabinSearchOrder(string originalCabin) => originalCabin switch
    {
        "Y" => ["Y", "W", "J", "F"],
        "W" => ["W", "J", "F"],
        "J" => ["J", "F"],
        "F" => ["F"],
        _ => [originalCabin]
    };
}

internal sealed class RebookReplacementOption
{
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public List<RebookReplacementLeg> Legs { get; init; } = [];
}

internal sealed class RebookReplacementLeg
{
    public string OfferId { get; init; } = string.Empty;
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public int SeatsAvailable { get; set; }
}
