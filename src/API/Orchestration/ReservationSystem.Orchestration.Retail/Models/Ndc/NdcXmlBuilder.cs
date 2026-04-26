using System.Text;
using System.Xml;
using System.Xml.Linq;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Models.Ndc;

/// <summary>
/// Builds IATA NDC 21.3 response XML documents (AirShoppingRS, OfferPriceRS) from
/// internal offer and flight DTOs.
///
/// AirShoppingRS mapping:
///   FlightItemDto        → FlightSegment in DataLists/FlightSegmentList
///   FlightItemDto        → OriginDestination in DataLists/OriginDestinationList
///   OfferItemDto         → Offer/OfferItem in OffersGroup/AirlineOffers
///   NdcPassengerType[]   → AnonymousTraveler in DataLists/AnonymousTravelerList
///
/// OfferPriceRS mapping:
///   OfferDetailDto       → DataLists flight and origin-destination elements
///   RepricedOfferItemDto → OfferItem elements inside PricedOffer
///
/// OfferID in the NDC response equals the internal OfferId GUID so NDC consumers
/// can reference it in subsequent OfferPrice and OrderCreate requests.
/// </summary>
public static class NdcXmlBuilder
{
    private const string AirShoppingRsNsUri = "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";
    private const string OfferPriceRsNsUri  = "http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRS";
    private const string OrderCreateRsNsUri = "http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRS";
    private const string CarrierCode = "AX";
    private const string CarrierName = "Apex Air";

    // Keep the old constant for existing code that references it via the private field alias below.
    private const string NsUri = AirShoppingRsNsUri;

    private readonly record struct PaxEntry(string Key, NdcPassengerType Pax);

    // ── Public entry point ────────────────────────────────────────────────────

    public static string BuildAirShoppingRS(
        OfferSearchResultDto searchResult,
        IReadOnlyList<NdcPassengerType> passengers,
        string responseId)
    {
        XNamespace ns = NsUri;

        // Assign stable keys for each flight and passenger type.
        var segKeys = searchResult.Flights
            .Select((f, i) => new { f.InventoryId, SegKey = $"SEG{i + 1}", OdKey = $"OD{i + 1}" })
            .ToDictionary(x => x.InventoryId, x => (x.SegKey, x.OdKey));

        var paxEntries = passengers
            .Select((p, i) => new PaxEntry($"PAX{i + 1}", p))
            .ToList();

        var allPaxRefs = string.Join(" ", paxEntries.Select(p => p.Key));

        var root = new XElement(ns + "IATA_AirShoppingRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildDocument(ns),
            BuildShoppingResponseId(ns, responseId),
            BuildOffersGroup(ns, searchResult, segKeys, allPaxRefs),
            BuildDataLists(ns, searchResult, segKeys, paxEntries));

        return SerialiseXml(root);
    }

    // ── Top-level sections ────────────────────────────────────────────────────

    private static XElement BuildDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildShoppingResponseId(XNamespace ns, string responseId) =>
        new(ns + "ShoppingResponseID",
            new XElement(ns + "ResponseID", responseId));

    private static XElement BuildOffersGroup(
        XNamespace ns,
        OfferSearchResultDto searchResult,
        Dictionary<Guid, (string SegKey, string OdKey)> segKeys,
        string allPaxRefs)
    {
        var airlineOffers = new XElement(ns + "AirlineOffers");

        foreach (var flight in searchResult.Flights)
        {
            if (!segKeys.TryGetValue(flight.InventoryId, out var keys)) continue;

            foreach (var offer in flight.Offers)
            {
                airlineOffers.Add(BuildOffer(ns, offer, keys.SegKey, allPaxRefs));
            }
        }

        return new XElement(ns + "OffersGroup", airlineOffers);
    }

    private static XElement BuildOffer(
        XNamespace ns,
        OfferItemDto offer,
        string segKey,
        string allPaxRefs)
    {
        var offerItemId = $"ITEM-{offer.OfferId:N}";
        var serviceId = $"SVC-{offer.OfferId:N}";

        return new XElement(ns + "Offer",
            new XElement(ns + "OfferID", offer.OfferId.ToString()),
            new XElement(ns + "OwnerCode", CarrierCode),
            new XElement(ns + "ValidatingCarrierCode", CarrierCode),
            new XElement(ns + "TotalPrice",
                new XElement(ns + "TotalAmount",
                    new XAttribute("CurCode", offer.CurrencyCode),
                    offer.TotalAmount.ToString("F2"))),
            new XElement(ns + "OfferItem",
                new XElement(ns + "OfferItemID", offerItemId),
                BuildTotalPriceDetail(ns, offer),
                new XElement(ns + "Service",
                    new XElement(ns + "ServiceID", serviceId),
                    new XElement(ns + "FlightRefs", segKey)),
                BuildFareDetail(ns, offer, segKey),
                new XElement(ns + "PassengerRefs", allPaxRefs)));
    }

    private static XElement BuildTotalPriceDetail(XNamespace ns, OfferItemDto offer) =>
        new(ns + "TotalPriceDetail",
            new XElement(ns + "TotalAmount",
                new XElement(ns + "SimpleCurrencyPrice",
                    new XAttribute("CurCode", offer.CurrencyCode),
                    offer.TotalAmount.ToString("F2"))),
            new XElement(ns + "BaseAmount",
                new XAttribute("CurCode", offer.CurrencyCode),
                offer.BaseFareAmount.ToString("F2")),
            new XElement(ns + "Taxes",
                new XElement(ns + "Total",
                    new XAttribute("CurCode", offer.CurrencyCode),
                    offer.TaxAmount.ToString("F2"))));

    private static XElement BuildFareDetail(XNamespace ns, OfferItemDto offer, string segKey)
    {
        var fareComponent = new XElement(ns + "FareComponent",
            new XElement(ns + "FareBasisCode",
                new XElement(ns + "Code", offer.FareBasisCode)),
            new XElement(ns + "CabinType",
                new XElement(ns + "CabinTypeCode",
                    new XElement(ns + "Code", MapCabinToNdc(offer.CabinCode)))),
            new XElement(ns + "SegmentRefs", segKey),
            BuildFareRules(ns, offer));

        return new XElement(ns + "FareDetail", fareComponent);
    }

    private static XElement BuildFareRules(XNamespace ns, OfferItemDto offer)
    {
        var penalties = new List<XElement>();

        if (!offer.IsRefundable)
        {
            penalties.Add(new XElement(ns + "Penalty",
                new XElement(ns + "Details",
                    new XElement(ns + "Detail",
                        new XElement(ns + "Type", "CANCELLATION"),
                        new XElement(ns + "Application", "ANYTIME"),
                        new XElement(ns + "CancelFeeInd", "true")))));
        }

        if (!offer.IsChangeable)
        {
            penalties.Add(new XElement(ns + "Penalty",
                new XElement(ns + "Details",
                    new XElement(ns + "Detail",
                        new XElement(ns + "Type", "CHANGE"),
                        new XElement(ns + "Application", "ANYTIME"),
                        new XElement(ns + "ChangeFeeInd", "true")))));
        }

        return penalties.Count > 0
            ? new XElement(ns + "FareRules", penalties)
            : new XElement(ns + "FareRules");
    }

    // ── DataLists ─────────────────────────────────────────────────────────────

    private static XElement BuildDataLists(
        XNamespace ns,
        OfferSearchResultDto searchResult,
        Dictionary<Guid, (string SegKey, string OdKey)> segKeys,
        IReadOnlyList<PaxEntry> paxEntries)
    {
        return new XElement(ns + "DataLists",
            BuildAnonymousTravelerList(ns, paxEntries),
            BuildFlightSegmentList(ns, searchResult, segKeys),
            BuildOriginDestinationList(ns, searchResult, segKeys));
    }

    private static XElement BuildAnonymousTravelerList(XNamespace ns, IReadOnlyList<PaxEntry> paxEntries)
    {
        var list = new XElement(ns + "AnonymousTravelerList");
        foreach (var entry in paxEntries)
        {
            list.Add(new XElement(ns + "AnonymousTraveler",
                new XElement(ns + "ObjectKey", entry.Key),
                new XElement(ns + "PTC", entry.Pax.Ptc),
                new XElement(ns + "Quantity", entry.Pax.Quantity.ToString())));
        }
        return list;
    }

    private static XElement BuildFlightSegmentList(
        XNamespace ns,
        OfferSearchResultDto searchResult,
        Dictionary<Guid, (string SegKey, string OdKey)> segKeys)
    {
        var list = new XElement(ns + "FlightSegmentList");

        foreach (var flight in searchResult.Flights)
        {
            if (!segKeys.TryGetValue(flight.InventoryId, out var keys)) continue;

            DateOnly.TryParse(flight.DepartureDate, out var depDate);
            var arrDate = depDate.AddDays(flight.ArrivalDayOffset);

            var (airlineId, flightNum) = SplitFlightNumber(flight.FlightNumber);
            var aircraftCode = MapAircraftType(flight.AircraftType);
            var durationIso = FormatDuration(flight.DurationMinutes);

            list.Add(new XElement(ns + "FlightSegment",
                new XElement(ns + "SegmentKey", keys.SegKey),
                new XElement(ns + "Departure",
                    new XElement(ns + "AirportCode", flight.Origin),
                    new XElement(ns + "Date", flight.DepartureDate),
                    new XElement(ns + "Time", flight.DepartureTime)),
                new XElement(ns + "Arrival",
                    new XElement(ns + "AirportCode", flight.Destination),
                    new XElement(ns + "Date", arrDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "Time", flight.ArrivalTime)),
                new XElement(ns + "MarketingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum),
                    new XElement(ns + "Name", CarrierName)),
                new XElement(ns + "OperatingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum)),
                new XElement(ns + "Equipment",
                    new XElement(ns + "AircraftCode", aircraftCode)),
                new XElement(ns + "FlightDetail",
                    new XElement(ns + "FlightDuration",
                        new XElement(ns + "Value", durationIso)))));
        }

        return list;
    }

    private static XElement BuildOriginDestinationList(
        XNamespace ns,
        OfferSearchResultDto searchResult,
        Dictionary<Guid, (string SegKey, string OdKey)> segKeys)
    {
        var list = new XElement(ns + "OriginDestinationList");

        foreach (var flight in searchResult.Flights)
        {
            if (!segKeys.TryGetValue(flight.InventoryId, out var keys)) continue;

            list.Add(new XElement(ns + "OriginDestination",
                new XElement(ns + "OriginDestinationKey", keys.OdKey),
                new XElement(ns + "DepartureCode", flight.Origin),
                new XElement(ns + "ArrivalCode", flight.Destination),
                new XElement(ns + "FlightReferences", keys.SegKey)));
        }

        return list;
    }

    // ── OfferPriceRS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an IATA NDC 21.3 OfferPriceRS XML document.
    /// PricedOffer contains one OfferItem per repriced fare returned by the Offer MS.
    /// OfferExpiration is taken from the stored offer ExpiresAt timestamp.
    /// DataLists carries the FlightSegment and OriginDestination for the priced flight.
    /// Travelers are included in AnonymousTravelerList when the request supplied them.
    /// </summary>
    public static string BuildOfferPriceRS(
        OfferDetailDto offerDetail,
        RepriceOfferDto repriceResult,
        IReadOnlyList<NdcPassengerType>? passengers)
    {
        XNamespace ns = OfferPriceRsNsUri;

        const string segKey = "SEG1";
        const string odKey = "OD1";

        var paxEntries = passengers?
            .Select((p, i) => new PaxEntry($"PAX{i + 1}", p))
            .ToList() ?? [];

        var allPaxRefs = paxEntries.Count > 0
            ? string.Join(" ", paxEntries.Select(p => p.Key))
            : null;

        var root = new XElement(ns + "IATA_OfferPriceRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildOpDocument(ns),
            BuildOpPricedOffer(ns, offerDetail, repriceResult, segKey, allPaxRefs),
            BuildOpDataLists(ns, offerDetail, segKey, odKey, paxEntries));

        return SerialiseXml(root);
    }

    private static XElement BuildOpDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildOpPricedOffer(
        XNamespace ns,
        OfferDetailDto offerDetail,
        RepriceOfferDto repriceResult,
        string segKey,
        string? allPaxRefs)
    {
        // Use the first repriced item's currency (all items share the same currency).
        var firstItem = repriceResult.Offers.FirstOrDefault();
        var currencyCode = firstItem?.CurrencyCode ?? "GBP";
        var totalAmount = repriceResult.Offers.Sum(o => o.TotalAmount);

        var pricedOffer = new XElement(ns + "PricedOffer",
            new XElement(ns + "OfferID", repriceResult.StoredOfferId.ToString()),
            new XElement(ns + "OwnerCode", CarrierCode),
            new XElement(ns + "ValidatingCarrierCode", CarrierCode),
            new XElement(ns + "TotalPrice",
                new XElement(ns + "TotalAmount",
                    new XAttribute("CurCode", currencyCode),
                    totalAmount.ToString("F2"))));

        foreach (var offer in repriceResult.Offers)
            pricedOffer.Add(BuildOpOfferItem(ns, offer, segKey, allPaxRefs));

        // OfferExpiration — ISO 8601 UTC timestamp parsed from the stored offer.
        if (!string.IsNullOrWhiteSpace(offerDetail.ExpiresAt) &&
            DateTimeOffset.TryParse(offerDetail.ExpiresAt, out var expiry))
        {
            pricedOffer.Add(new XElement(ns + "OfferExpiration",
                new XElement(ns + "DateTime",
                    expiry.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))));
        }

        return pricedOffer;
    }

    private static XElement BuildOpOfferItem(
        XNamespace ns,
        RepricedOfferItemDto offer,
        string segKey,
        string? allPaxRefs)
    {
        var offerItemId = $"ITEM-{offer.OfferId:N}";
        var serviceId = $"SVC-{offer.OfferId:N}";

        var item = new XElement(ns + "OfferItem",
            new XElement(ns + "OfferItemID", offerItemId),
            BuildOpTotalPriceDetail(ns, offer),
            new XElement(ns + "Service",
                new XElement(ns + "ServiceID", serviceId),
                new XElement(ns + "FlightRefs", segKey)),
            BuildOpFareDetail(ns, offer, segKey));

        if (!string.IsNullOrWhiteSpace(allPaxRefs))
            item.Add(new XElement(ns + "PassengerRefs", allPaxRefs));

        return item;
    }

    private static XElement BuildOpTotalPriceDetail(XNamespace ns, RepricedOfferItemDto offer)
    {
        var taxes = new XElement(ns + "Taxes",
            new XElement(ns + "Total",
                new XAttribute("CurCode", offer.CurrencyCode),
                offer.TaxAmount.ToString("F2")));

        // Include per-tax breakdown when available (enhances IATA NDC compliance).
        if (offer.TaxLines is { Count: > 0 })
        {
            var breakdown = new XElement(ns + "Breakdown");
            foreach (var tax in offer.TaxLines)
            {
                breakdown.Add(new XElement(ns + "Tax",
                    new XElement(ns + "Amount",
                        new XAttribute("CurCode", offer.CurrencyCode),
                        tax.Amount.ToString("F2")),
                    new XElement(ns + "TaxCode", tax.Code)));
            }
            taxes.Add(breakdown);
        }

        return new XElement(ns + "TotalPriceDetail",
            new XElement(ns + "TotalAmount",
                new XElement(ns + "SimpleCurrencyPrice",
                    new XAttribute("CurCode", offer.CurrencyCode),
                    offer.TotalAmount.ToString("F2"))),
            new XElement(ns + "BaseAmount",
                new XAttribute("CurCode", offer.CurrencyCode),
                offer.BaseFareAmount.ToString("F2")),
            taxes);
    }

    private static XElement BuildOpFareDetail(XNamespace ns, RepricedOfferItemDto offer, string segKey)
    {
        var penalties = new List<XElement>();

        if (!offer.IsRefundable)
        {
            penalties.Add(new XElement(ns + "Penalty",
                new XElement(ns + "Details",
                    new XElement(ns + "Detail",
                        new XElement(ns + "Type", "CANCELLATION"),
                        new XElement(ns + "Application", "ANYTIME"),
                        new XElement(ns + "CancelFeeInd", "true")))));
        }

        if (!offer.IsChangeable)
        {
            penalties.Add(new XElement(ns + "Penalty",
                new XElement(ns + "Details",
                    new XElement(ns + "Detail",
                        new XElement(ns + "Type", "CHANGE"),
                        new XElement(ns + "Application", "ANYTIME"),
                        new XElement(ns + "ChangeFeeInd", "true")))));
        }

        var fareRules = penalties.Count > 0
            ? new XElement(ns + "FareRules", penalties)
            : new XElement(ns + "FareRules");

        return new XElement(ns + "FareDetail",
            new XElement(ns + "FareComponent",
                new XElement(ns + "FareBasisCode",
                    new XElement(ns + "Code", offer.FareBasisCode)),
                new XElement(ns + "CabinType",
                    new XElement(ns + "CabinTypeCode",
                        new XElement(ns + "Code", MapCabinToNdc(offer.CabinCode)))),
                new XElement(ns + "SegmentRefs", segKey),
                fareRules));
    }

    private static XElement BuildOpDataLists(
        XNamespace ns,
        OfferDetailDto offerDetail,
        string segKey,
        string odKey,
        IReadOnlyList<PaxEntry> paxEntries)
    {
        var dataLists = new XElement(ns + "DataLists",
            BuildOpFlightSegmentList(ns, offerDetail, segKey),
            BuildOpOriginDestinationList(ns, offerDetail, segKey, odKey));

        if (paxEntries.Count > 0)
        {
            var travelerList = new XElement(ns + "AnonymousTravelerList");
            foreach (var entry in paxEntries)
            {
                travelerList.Add(new XElement(ns + "AnonymousTraveler",
                    new XElement(ns + "ObjectKey", entry.Key),
                    new XElement(ns + "PTC", entry.Pax.Ptc),
                    new XElement(ns + "Quantity", entry.Pax.Quantity.ToString())));
            }
            dataLists.Add(travelerList);
        }

        return dataLists;
    }

    private static XElement BuildOpFlightSegmentList(XNamespace ns, OfferDetailDto flight, string segKey)
    {
        DateOnly.TryParse(flight.DepartureDate, out var depDate);
        var arrDate = depDate.AddDays(flight.ArrivalDayOffset);

        var (airlineId, flightNum) = SplitFlightNumber(flight.FlightNumber);
        var aircraftCode = MapAircraftType(flight.AircraftType);

        return new XElement(ns + "FlightSegmentList",
            new XElement(ns + "FlightSegment",
                new XElement(ns + "SegmentKey", segKey),
                new XElement(ns + "Departure",
                    new XElement(ns + "AirportCode", flight.Origin),
                    new XElement(ns + "Date", flight.DepartureDate),
                    new XElement(ns + "Time", flight.DepartureTime)),
                new XElement(ns + "Arrival",
                    new XElement(ns + "AirportCode", flight.Destination),
                    new XElement(ns + "Date", arrDate.ToString("yyyy-MM-dd")),
                    new XElement(ns + "Time", flight.ArrivalTime)),
                new XElement(ns + "MarketingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum),
                    new XElement(ns + "Name", CarrierName)),
                new XElement(ns + "OperatingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum)),
                new XElement(ns + "Equipment",
                    new XElement(ns + "AircraftCode", aircraftCode))));
    }

    private static XElement BuildOpOriginDestinationList(
        XNamespace ns, OfferDetailDto flight, string segKey, string odKey) =>
        new(ns + "OriginDestinationList",
            new XElement(ns + "OriginDestination",
                new XElement(ns + "OriginDestinationKey", odKey),
                new XElement(ns + "DepartureCode", flight.Origin),
                new XElement(ns + "ArrivalCode", flight.Destination),
                new XElement(ns + "FlightReferences", segKey)));

    // ── OrderCreateRS ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an IATA NDC 21.3 OrderCreateRS XML document from a confirmed OrderResponse.
    ///
    /// Structure:
    ///   Document                    — schema version 21.3
    ///   Response/Order              — OrderID, BookingRef, StatusCode, TotalAmount
    ///   Response/Order/OrderItem    — one per confirmed flight item (Price, FareDetail)
    ///   Response/Order/TicketDocInfo — one per issued e-ticket per passenger
    ///   Response/DataLists          — FlightSegmentList, OriginDestinationList, PaxList
    /// </summary>
    public static string BuildOrderCreateRS(
        OrderResponse order,
        IReadOnlyList<NdcOrderCreatePassenger> passengers)
    {
        XNamespace ns = OrderCreateRsNsUri;

        // Assign stable NDC segment keys indexed by position.
        var segKeyMap = order.FlightSegments
            .Select((s, i) => new { s.SegmentId, SegKey = $"SEG{i + 1}", OdKey = $"OD{i + 1}" })
            .ToDictionary(x => x.SegmentId, x => (x.SegKey, x.OdKey));

        var root = new XElement(ns + "IATA_OrderCreateRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildOcDocument(ns),
            BuildOcResponse(ns, order, segKeyMap, passengers));

        return SerialiseXml(root);
    }

    private static XElement BuildOcDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildOcResponse(
        XNamespace ns,
        OrderResponse order,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap,
        IReadOnlyList<NdcOrderCreatePassenger> passengers)
    {
        var orderEl = new XElement(ns + "Order",
            new XElement(ns + "OrderID", order.OrderId.ToString()),
            BuildOcBookingRef(ns, order.BookingReference),
            new XElement(ns + "StatusCode", "ISSUED"),
            new XElement(ns + "TotalAmount",
                new XAttribute("CurCode", order.Currency),
                order.TotalAmount.ToString("F2")));

        // Flight OrderItems
        foreach (var item in order.OrderItems.Where(i => i.Type == "Flight"))
        {
            segKeyMap.TryGetValue(item.SegmentRef, out var keys);
            var segKey = keys.SegKey ?? item.SegmentRef;
            var cabinCode = order.FlightSegments
                .FirstOrDefault(s => s.SegmentId == item.SegmentRef)?.CabinCode ?? "Y";

            orderEl.Add(BuildOcOrderItem(ns, item, segKey, cabinCode, order.Currency));
        }

        // TicketDocInfo — collect all e-tickets across flight order items
        var allETickets = order.OrderItems
            .Where(i => i.Type == "Flight" && i.ETickets is { Count: > 0 })
            .SelectMany(i => i.ETickets!)
            .GroupBy(t => t.PassengerId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in allETickets)
        {
            var ticketDocInfo = new XElement(ns + "TicketDocInfo",
                new XElement(ns + "PaxRefID", group.Key));

            foreach (var eTicket in group)
            {
                ticketDocInfo.Add(new XElement(ns + "TicketDocument",
                    new XElement(ns + "TicketDocNbr", eTicket.ETicketNumber),
                    new XElement(ns + "Type", "T"),
                    new XElement(ns + "ReportingType", "BSP")));
            }

            orderEl.Add(ticketDocInfo);
        }

        return new XElement(ns + "Response",
            orderEl,
            BuildOcDataLists(ns, order, segKeyMap, passengers));
    }

    private static XElement BuildOcBookingRef(XNamespace ns, string bookingReference) =>
        new(ns + "BookingRef",
            new XElement(ns + "BookingEntity",
                new XElement(ns + "Carrier",
                    new XElement(ns + "AirlineDesigCode", CarrierCode))),
            new XElement(ns + "ID", bookingReference));

    private static XElement BuildOcOrderItem(
        XNamespace ns,
        ConfirmedOrderItem item,
        string segKey,
        string cabinCode,
        string currency)
    {
        var orderItem = new XElement(ns + "OrderItem",
            new XElement(ns + "OrderItemID", item.OrderItemId),
            new XElement(ns + "StatusCode", "PAYMENT_DONE"),
            new XElement(ns + "FlightRefs", segKey));

        foreach (var paxRef in item.PassengerRefs)
            orderItem.Add(new XElement(ns + "PaxRefID", paxRef));

        orderItem.Add(new XElement(ns + "Price",
            new XElement(ns + "TotalAmount",
                new XAttribute("CurCode", currency),
                item.TotalPrice.ToString("F2")),
            new XElement(ns + "BaseAmount",
                new XAttribute("CurCode", currency),
                (item.TotalPrice - item.Taxes).ToString("F2")),
            new XElement(ns + "Taxes",
                new XElement(ns + "Total",
                    new XAttribute("CurCode", currency),
                    item.Taxes.ToString("F2")))));

        if (!string.IsNullOrWhiteSpace(item.FareBasisCode))
        {
            orderItem.Add(new XElement(ns + "FareDetail",
                new XElement(ns + "FareComponent",
                    new XElement(ns + "FareBasisCode",
                        new XElement(ns + "Code", item.FareBasisCode)),
                    new XElement(ns + "CabinType",
                        new XElement(ns + "CabinTypeCode",
                            new XElement(ns + "Code", MapCabinToNdc(cabinCode)))),
                    new XElement(ns + "SegmentRefs", segKey))));
        }

        return orderItem;
    }

    private static XElement BuildOcDataLists(
        XNamespace ns,
        OrderResponse order,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap,
        IReadOnlyList<NdcOrderCreatePassenger> passengers)
    {
        return new XElement(ns + "DataLists",
            BuildOcFlightSegmentList(ns, order.FlightSegments, segKeyMap),
            BuildOcOriginDestinationList(ns, order.FlightSegments, segKeyMap),
            BuildOcPaxList(ns, order.Passengers, passengers));
    }

    private static XElement BuildOcFlightSegmentList(
        XNamespace ns,
        IReadOnlyList<ConfirmedFlightSegment> segments,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap)
    {
        var list = new XElement(ns + "FlightSegmentList");

        foreach (var seg in segments)
        {
            if (!segKeyMap.TryGetValue(seg.SegmentId, out var keys)) continue;

            var (depDate, depTime) = SplitDateTime(seg.DepartureDateTime);
            var (arrDate, arrTime) = SplitDateTime(seg.ArrivalDateTime);
            var (airlineId, flightNum) = SplitFlightNumber(seg.FlightNumber);
            var aircraftCode = MapAircraftType(seg.AircraftType);

            list.Add(new XElement(ns + "FlightSegment",
                new XElement(ns + "SegmentKey", keys.SegKey),
                new XElement(ns + "Departure",
                    new XElement(ns + "AirportCode", seg.Origin),
                    new XElement(ns + "Date", depDate),
                    new XElement(ns + "Time", depTime)),
                new XElement(ns + "Arrival",
                    new XElement(ns + "AirportCode", seg.Destination),
                    new XElement(ns + "Date", arrDate),
                    new XElement(ns + "Time", arrTime)),
                new XElement(ns + "MarketingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum),
                    new XElement(ns + "Name", CarrierName)),
                new XElement(ns + "OperatingCarrier",
                    new XElement(ns + "AirlineID", airlineId),
                    new XElement(ns + "FlightNumber", flightNum)),
                new XElement(ns + "Equipment",
                    new XElement(ns + "AircraftCode", aircraftCode))));
        }

        return list;
    }

    private static XElement BuildOcOriginDestinationList(
        XNamespace ns,
        IReadOnlyList<ConfirmedFlightSegment> segments,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap)
    {
        var list = new XElement(ns + "OriginDestinationList");

        foreach (var seg in segments)
        {
            if (!segKeyMap.TryGetValue(seg.SegmentId, out var keys)) continue;

            list.Add(new XElement(ns + "OriginDestination",
                new XElement(ns + "OriginDestinationKey", keys.OdKey),
                new XElement(ns + "DepartureCode", seg.Origin),
                new XElement(ns + "ArrivalCode", seg.Destination),
                new XElement(ns + "FlightReferences", keys.SegKey)));
        }

        return list;
    }

    private static XElement BuildOcPaxList(
        XNamespace ns,
        IReadOnlyList<ConfirmedPassenger> confirmedPassengers,
        IReadOnlyList<NdcOrderCreatePassenger> requestPassengers)
    {
        var list = new XElement(ns + "PaxList");

        // Use confirmed passengers as the authoritative source; supplement with request
        // PTC when confirmed passenger type is unavailable.
        var ptcByPaxId = requestPassengers.ToDictionary(
            p => p.PaxId, p => p.Ptc, StringComparer.OrdinalIgnoreCase);

        foreach (var pax in confirmedPassengers)
        {
            ptcByPaxId.TryGetValue(pax.PassengerId, out var ptc);

            list.Add(new XElement(ns + "Pax",
                new XElement(ns + "PaxID", pax.PassengerId),
                new XElement(ns + "PTC", ptc ?? pax.Type),
                new XElement(ns + "Individual",
                    new XElement(ns + "GivenName", pax.GivenName.ToUpperInvariant()),
                    new XElement(ns + "Surname", pax.Surname.ToUpperInvariant()))));
        }

        return list;
    }

    // ── Helpers: DateTime splitting ───────────────────────────────────────────

    /// <summary>
    /// Splits an ISO 8601 UTC datetime string (e.g. "2026-07-15T10:00:00Z") into
    /// a date string ("2026-07-15") and time string ("10:00").
    /// </summary>
    private static (string Date, string Time) SplitDateTime(string isoDateTime)
    {
        if (string.IsNullOrWhiteSpace(isoDateTime))
            return (string.Empty, string.Empty);

        if (DateTimeOffset.TryParse(isoDateTime, out var dto))
        {
            return (dto.UtcDateTime.ToString("yyyy-MM-dd"), dto.UtcDateTime.ToString("HH:mm"));
        }

        // Fallback: split on T
        var tIdx = isoDateTime.IndexOf('T');
        if (tIdx > 0)
        {
            var datePart = isoDateTime[..tIdx];
            var timePart = isoDateTime[(tIdx + 1)..].TrimEnd('Z');
            if (timePart.Length > 5) timePart = timePart[..5];
            return (datePart, timePart);
        }

        return (isoDateTime, string.Empty);
    }

    // ── Shared XML serialisation ──────────────────────────────────────────────

    private static string SerialiseXml(XElement root)
    {
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var ms = new MemoryStream();
        using var writer = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false
        });
        doc.WriteTo(writer);
        writer.Flush();
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(ms.ToArray());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps internal cabin codes (Y/J/W/F) to NDC CabinTypeCode values.
    /// NDC uses M=Economy, W=PremiumEconomy, C=Business, F=First.
    /// </summary>
    private static string MapCabinToNdc(string cabinCode) => cabinCode switch
    {
        "Y" => "M",
        "W" => "W",
        "J" => "C",
        "F" => "F",
        _ => "M"
    };

    /// <summary>
    /// Splits an Apex Air flight number (e.g. "AX001") into IATA carrier code and
    /// numeric part (e.g. AirlineID="AX", FlightNumber="001").
    /// </summary>
    private static (string AirlineId, string FlightNum) SplitFlightNumber(string flightNumber)
    {
        if (flightNumber.Length > 2
            && char.IsLetter(flightNumber[0])
            && char.IsLetter(flightNumber[1]))
        {
            return (flightNumber[..2], flightNumber[2..]);
        }

        return (CarrierCode, flightNumber);
    }

    /// <summary>
    /// Maps internal 4-char aircraft type codes to IATA 3-char codes used in NDC Equipment.
    /// </summary>
    private static string MapAircraftType(string aircraftType) => aircraftType switch
    {
        "A351" => "351",
        "B789" => "789",
        "A339" => "339",
        _ => aircraftType.Length >= 3 ? aircraftType[^3..] : aircraftType
    };

    /// <summary>
    /// Formats flight duration minutes as an ISO 8601 duration string (e.g. PT7H10M).
    /// </summary>
    private static string FormatDuration(int minutes)
    {
        if (minutes <= 0) return "PT0M";
        var h = minutes / 60;
        var m = minutes % 60;
        return (h, m) switch
        {
            (> 0, > 0) => $"PT{h}H{m}M",
            (> 0, 0) => $"PT{h}H",
            _ => $"PT{m}M"
        };
    }
}
