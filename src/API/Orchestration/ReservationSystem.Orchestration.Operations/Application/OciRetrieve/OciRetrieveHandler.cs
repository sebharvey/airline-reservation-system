using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciRetrieve;

public sealed record OciRetrieveQuery(
    string BookingReference,
    string FirstName,
    string LastName,
    string DepartureAirport,
    string? LoyaltyNumber);

public sealed record OciPassengerResult(
    string PassengerId,
    string TicketNumber,
    string GivenName,
    string Surname,
    string PassengerTypeCode,
    OciTravelDocument? TravelDocument);

public sealed record OciTravelDocument(
    string? Type,
    string? Number,
    string? IssuingCountry,
    string? Nationality,
    string? IssueDate,
    string? ExpiryDate);

public sealed record OciRetrieveResult(
    string BookingReference,
    bool CheckInEligible,
    bool IsStandby,
    bool AlreadyCheckedIn,
    IReadOnlyList<OciPassengerResult> Passengers);

public sealed class OciRetrieveHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ILogger<OciRetrieveHandler> _logger;

    public OciRetrieveHandler(
        OrderServiceClient orderServiceClient,
        CustomerServiceClient customerServiceClient,
        ILogger<OciRetrieveHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _customerServiceClient = customerServiceClient;
        _logger = logger;
    }

    public async Task<OciRetrieveResult?> HandleAsync(OciRetrieveQuery query, CancellationToken ct)
    {
        // Retrieve order from Order MS using surname (lastName)
        var order = await _orderServiceClient.RetrieveOrderAsync(query.BookingReference, query.LastName, ct);
        if (order is null)
        {
            _logger.LogWarning("OCI retrieve: order not found for {BookingReference}", query.BookingReference);
            return null;
        }

        // Parse passengers and e-tickets from orderData
        var passengers = new List<OciPassengerResult>();

        if (order.OrderData is not JsonElement orderDataEl || orderDataEl.ValueKind != JsonValueKind.Object)
            return new OciRetrieveResult(query.BookingReference, true, false, false, passengers);

        var bookingType = orderDataEl.TryGetProperty("bookingType", out var btEl) ? btEl.GetString() : null;
        var isStandby = string.Equals(bookingType, "Standby", StringComparison.OrdinalIgnoreCase);

        // Validate that the supplied departure airport exists on the booking's itinerary.
        // This check must live in the API so that direct API callers cannot bypass it.
        if (!string.IsNullOrWhiteSpace(query.DepartureAirport))
        {
            var airportFound = false;
            if (orderDataEl.TryGetProperty("orderItems", out var orderItemsEl) &&
                orderItemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in orderItemsEl.EnumerateArray())
                {
                    var productType = item.TryGetProperty("productType", out var ptEl) ? ptEl.GetString() : null;
                    if (!string.Equals(productType, "FLIGHT", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var origin = item.TryGetProperty("origin", out var origEl) ? origEl.GetString() : null;
                    if (string.Equals(origin, query.DepartureAirport, StringComparison.OrdinalIgnoreCase))
                    {
                        airportFound = true;
                        break;
                    }
                }
            }
            if (!airportFound)
                throw new InvalidOperationException(
                    $"Departure airport '{query.DepartureAirport.ToUpperInvariant()}' is not on this booking's itinerary.");
        }

        // Build a map from passengerId → eTicketNumber
        var ticketMap = CheckInHelper.ParsePaxToTicketMap(order.OrderData);

        // Optionally fetch loyalty profile to pre-fill passport data
        CustomerProfile? loyaltyProfile = null;
        if (!string.IsNullOrWhiteSpace(query.LoyaltyNumber))
        {
            try { loyaltyProfile = await _customerServiceClient.GetByLoyaltyNumberAsync(query.LoyaltyNumber, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch loyalty profile for {LoyaltyNumber}", query.LoyaltyNumber); }
        }

        // Extract passengers from dataLists.passengers
        if (orderDataEl.TryGetProperty("dataLists", out var dataListsEl) &&
            dataListsEl.TryGetProperty("passengers", out var paxArrayEl) &&
            paxArrayEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var pax in paxArrayEl.EnumerateArray())
            {
                var passengerId = pax.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
                var givenName = pax.TryGetProperty("givenName", out var gnEl) ? gnEl.GetString() ?? "" : "";
                var surname = pax.TryGetProperty("surname", out var snEl) ? snEl.GetString() ?? "" : "";
                var paxType = pax.TryGetProperty("type", out var ptEl) ? ptEl.GetString() ?? "ADT" : "ADT";
                var loyaltyNumber = pax.TryGetProperty("loyaltyNumber", out var lnEl) ? lnEl.GetString() : null;

                if (passengerId is null) continue;

                if (!ticketMap.TryGetValue(passengerId, out var ticketNumber))
                    throw new InvalidOperationException(
                        $"Booking {query.BookingReference} has no e-ticket number for passenger {passengerId}. Check-in cannot proceed.");

                OciTravelDocument? travelDoc = null;

                // Try to get existing travel doc from order (use first document in array)
                if (pax.TryGetProperty("docs", out var tdsEl) &&
                    tdsEl.ValueKind == JsonValueKind.Array &&
                    tdsEl.GetArrayLength() > 0)
                {
                    var tdEl = tdsEl[0];
                    travelDoc = new OciTravelDocument(
                        Type: tdEl.TryGetProperty("type", out var tyEl) ? tyEl.GetString() : null,
                        Number: tdEl.TryGetProperty("number", out var numEl) ? numEl.GetString() : null,
                        IssuingCountry: tdEl.TryGetProperty("issuingCountry", out var icEl) ? icEl.GetString() : null,
                        Nationality: tdEl.TryGetProperty("nationality", out var natEl) ? natEl.GetString() : null,
                        IssueDate: tdEl.TryGetProperty("issueDate", out var idEl) ? idEl.GetString() : null,
                        ExpiryDate: tdEl.TryGetProperty("expiryDate", out var edEl) ? edEl.GetString() : null);
                }

                // If this passenger's loyalty number matches the fetched profile, pre-fill passport
                if (travelDoc is null && loyaltyProfile is not null &&
                    !string.IsNullOrWhiteSpace(loyaltyNumber) &&
                    string.Equals(loyaltyNumber, query.LoyaltyNumber, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(loyaltyProfile.PassportNumber))
                {
                    travelDoc = new OciTravelDocument(
                        Type: "PASSPORT",
                        Number: loyaltyProfile.PassportNumber,
                        IssuingCountry: loyaltyProfile.PassportIssuer,
                        Nationality: loyaltyProfile.Nationality,
                        IssueDate: loyaltyProfile.PassportIssueDate,
                        ExpiryDate: loyaltyProfile.PassportExpiryDate);
                }

                passengers.Add(new OciPassengerResult(
                    PassengerId: passengerId,
                    TicketNumber: ticketNumber!,
                    GivenName: givenName,
                    Surname: surname,
                    PassengerTypeCode: paxType,
                    TravelDocument: travelDoc));
            }
        }

        var alreadyCheckedIn = false;
        if (!string.IsNullOrWhiteSpace(query.DepartureAirport) &&
            orderDataEl.TryGetProperty("orderItems", out var checkinItemsEl) &&
            checkinItemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in checkinItemsEl.EnumerateArray())
            {
                var origin = item.TryGetProperty("origin", out var origEl) ? origEl.GetString() : null;
                if (!string.Equals(origin, query.DepartureAirport, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (item.TryGetProperty("checkin", out var checkinEl) &&
                    checkinEl.TryGetProperty("status", out var statusEl) &&
                    string.Equals(statusEl.GetString(), "CheckedIn", StringComparison.OrdinalIgnoreCase))
                {
                    alreadyCheckedIn = true;
                    break;
                }
            }
        }

        return new OciRetrieveResult(query.BookingReference, true, isStandby, alreadyCheckedIn, passengers);
    }
}
