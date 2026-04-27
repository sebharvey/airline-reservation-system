using System.Text;
using System.Xml;
using System.Xml.Linq;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;
using ReservationSystem.Orchestration.Retail.Application.NdcSeatAvailability;
using ReservationSystem.Orchestration.Retail.Application.NdcServiceList;
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
    private const string ServiceListRsNsUri    = "http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRS";
    private const string OrderRetrieveRsNsUri      = "http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderRetrieveRS";
    private const string SeatAvailabilityRsNsUri   = "http://www.iata.org/IATA/2015/00/2021.3/IATA_SeatAvailabilityRS";
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

    // ── ServiceListRS ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an IATA NDC 21.3 ServiceListRS XML document from active SSR catalogue entries.
    ///
    /// Structure:
    ///   Document                                    — schema version 21.3
    ///   Response/ServiceList/ALaCarteOffer          — one offer block owned by AX
    ///   ALaCarteOffer/ALaCarteOfferItem             — one item per active SSR code
    ///     OfferItemID                               — SLI-{SsrCode}
    ///     Eligibility                               — FlightAssociationType=All, PaxAssociationType=All
    ///     Service/ServiceID                         — SVC-{SsrCode}
    ///     Service/Name                              — human-readable label
    ///     Service/ServiceCode/Code                  — four-char IATA SSR code
    ///     Service/ServiceCode/ServiceType           — SSR
    ///     Service/ServiceGroup/Code                 — NDC group derived from SSR category
    ///     UnitPriceDetail/TotalAmount               — 0.00 GBP (SSRs are complimentary)
    ///   Response/DataLists/ServiceDefinitionList    — one ServiceDefinition per SSR
    /// </summary>
    public static string BuildServiceListRS(NdcServiceListResult result)
    {
        XNamespace ns = ServiceListRsNsUri;

        var root = new XElement(ns + "IATA_ServiceListRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildSlDocument(ns),
            BuildSlResponse(ns, result));

        return SerialiseXml(root);
    }

    private static XElement BuildSlDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildSlResponse(XNamespace ns, NdcServiceListResult result)
    {
        var aLaCarteOffer = new XElement(ns + "ALaCarteOffer",
            new XElement(ns + "Owner", CarrierCode));

        foreach (var svc in result.Services)
            aLaCarteOffer.Add(BuildSlALaCarteOfferItem(ns, svc));

        var serviceList = new XElement(ns + "ServiceList", aLaCarteOffer);

        var response = new XElement(ns + "Response", serviceList);

        // DataLists/ServiceDefinitionList provides richer descriptions for consumers.
        if (result.Services.Count > 0)
            response.Add(BuildSlDataLists(ns, result.Services));

        return response;
    }

    private static XElement BuildSlALaCarteOfferItem(XNamespace ns, NdcSsrServiceItem svc)
    {
        var offerItemId = $"SLI-{svc.SsrCode}";
        var serviceId   = $"SVC-{svc.SsrCode}";

        return new XElement(ns + "ALaCarteOfferItem",
            new XElement(ns + "OfferItemID", offerItemId),
            new XElement(ns + "Eligibility",
                new XElement(ns + "FlightAssociationType", "All"),
                new XElement(ns + "PaxAssociationType",    "All")),
            new XElement(ns + "Service",
                new XElement(ns + "ServiceID", serviceId),
                new XElement(ns + "Name",      svc.Label),
                new XElement(ns + "ServiceCode",
                    new XElement(ns + "Code",        svc.SsrCode),
                    new XElement(ns + "ServiceType", "SSR")),
                new XElement(ns + "ServiceGroup",
                    new XElement(ns + "Code", MapSsrCategoryToNdcGroup(svc.Category)))),
            new XElement(ns + "UnitPriceDetail",
                new XElement(ns + "TotalAmount",
                    new XElement(ns + "SimpleCurrencyPrice",
                        new XAttribute("CurCode", "GBP"),
                        "0.00"))));
    }

    private static XElement BuildSlDataLists(XNamespace ns, IReadOnlyList<NdcSsrServiceItem> services)
    {
        var sdList = new XElement(ns + "ServiceDefinitionList");

        foreach (var svc in services)
        {
            sdList.Add(new XElement(ns + "ServiceDefinition",
                new XElement(ns + "ServiceDefinitionID", $"SD-{svc.SsrCode}"),
                new XElement(ns + "Name",                svc.Label),
                new XElement(ns + "Desc",                svc.Label),
                new XElement(ns + "ServiceCode",
                    new XElement(ns + "Code",        svc.SsrCode),
                    new XElement(ns + "ServiceType", "SSR")),
                new XElement(ns + "Category",        MapSsrCategoryToNdcGroup(svc.Category))));
        }

        return new XElement(ns + "DataLists", sdList);
    }

    /// <summary>
    /// Maps the internal SSR catalogue category label to an NDC ServiceGroup code.
    /// NDC uses short uppercase group codes; the mapping is best-effort against the
    /// free-text category values stored in the SSR catalogue.
    /// </summary>
    private static string MapSsrCategoryToNdcGroup(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "OTHER";

        return category.ToUpperInvariant() switch
        {
            var c when c.Contains("MEAL")         => "MEAL",
            var c when c.Contains("DIET")         => "MEAL",
            var c when c.Contains("FOOD")         => "MEAL",
            var c when c.Contains("WHEEL")        => "ACCESSIBILITY",
            var c when c.Contains("MOBIL")        => "ACCESSIBILITY",
            var c when c.Contains("DISAB")        => "ACCESSIBILITY",
            var c when c.Contains("MEDICAL")      => "MEDICAL",
            var c when c.Contains("MED")          => "MEDICAL",
            var c when c.Contains("INFANT")       => "INFANT",
            var c when c.Contains("BASSINET")     => "INFANT",
            var c when c.Contains("BAGGAGE")      => "BAGGAGE",
            var c when c.Contains("BAG")          => "BAGGAGE",
            var c when c.Contains("PET")          => "PET",
            var c when c.Contains("SPORT")        => "SPORT",
            _                                     => "OTHER"
        };
    }

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

    // ── OrderRetrieveRS ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds an IATA NDC 21.3 OrderRetrieveRS XML document from a managed order response.
    ///
    /// Structure:
    ///   Document                    — schema version 21.3
    ///   Response/Order              — OrderID, BookingRef, StatusCode, TotalAmount
    ///   Response/Order/OrderItem    — one per confirmed flight item (Price, FareDetail)
    ///   Response/Order/TicketDocInfo — one per issued e-ticket per passenger
    ///   Response/DataLists          — FlightSegmentList, OriginDestinationList, PaxList
    ///
    /// StatusCode mapping: Confirmed→ISSUED, Cancelled→CANCELLED, all others→OPEN.
    /// </summary>
    public static string BuildOrderRetrieveRS(ManagedOrderResponse order)
    {
        XNamespace ns = OrderRetrieveRsNsUri;

        var segKeyMap = order.FlightSegments
            .Select((s, i) => new { s.SegmentId, SegKey = $"SEG{i + 1}", OdKey = $"OD{i + 1}" })
            .ToDictionary(x => x.SegmentId, x => (x.SegKey, x.OdKey));

        var root = new XElement(ns + "IATA_OrderRetrieveRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildOrDocument(ns),
            BuildOrResponse(ns, order, segKeyMap));

        return SerialiseXml(root);
    }

    private static XElement BuildOrDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildOrResponse(
        XNamespace ns,
        ManagedOrderResponse order,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap)
    {
        var statusCode = order.OrderStatus switch
        {
            var s when string.Equals(s, "Confirmed", StringComparison.OrdinalIgnoreCase) => "ISSUED",
            var s when string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase) => "CANCELLED",
            _ => "OPEN"
        };

        var orderEl = new XElement(ns + "Order",
            new XElement(ns + "OrderID", order.OrderId),
            BuildOrBookingRef(ns, order.BookingReference),
            new XElement(ns + "StatusCode", statusCode),
            new XElement(ns + "TotalAmount",
                new XAttribute("CurCode", order.Currency),
                order.TotalAmount.ToString("F2")));

        // Flight OrderItems
        foreach (var item in order.OrderItems.Where(
            i => string.Equals(i.Type, "Flight", StringComparison.OrdinalIgnoreCase)))
        {
            segKeyMap.TryGetValue(item.SegmentRef, out var keys);
            var segKey = keys.SegKey ?? item.SegmentRef;
            var cabinCode = order.FlightSegments
                .FirstOrDefault(s => s.SegmentId == item.SegmentRef)?.CabinCode ?? "Y";

            orderEl.Add(BuildOrOrderItem(ns, item, segKey, cabinCode, order.Currency));
        }

        // TicketDocInfo — one block per passenger, carrying all their e-ticket numbers
        var eTicketsByPax = order.OrderItems
            .Where(i => string.Equals(i.Type, "Flight", StringComparison.OrdinalIgnoreCase)
                        && i.ETickets is { Count: > 0 })
            .SelectMany(i => i.ETickets)
            .GroupBy(t => t.PassengerId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in eTicketsByPax)
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
            BuildOrDataLists(ns, order, segKeyMap));
    }

    private static XElement BuildOrBookingRef(XNamespace ns, string bookingReference) =>
        new(ns + "BookingRef",
            new XElement(ns + "BookingEntity",
                new XElement(ns + "Carrier",
                    new XElement(ns + "AirlineDesigCode", CarrierCode))),
            new XElement(ns + "ID", bookingReference));

    private static XElement BuildOrOrderItem(
        XNamespace ns,
        ManagedOrderItem item,
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

    private static XElement BuildOrDataLists(
        XNamespace ns,
        ManagedOrderResponse order,
        Dictionary<string, (string SegKey, string OdKey)> segKeyMap)
    {
        return new XElement(ns + "DataLists",
            BuildOrFlightSegmentList(ns, order.FlightSegments, segKeyMap),
            BuildOrOriginDestinationList(ns, order.FlightSegments, segKeyMap),
            BuildOrPaxList(ns, order.Passengers));
    }

    private static XElement BuildOrFlightSegmentList(
        XNamespace ns,
        IReadOnlyList<ManagedFlightSegment> segments,
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

    private static XElement BuildOrOriginDestinationList(
        XNamespace ns,
        IReadOnlyList<ManagedFlightSegment> segments,
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

    private static XElement BuildOrPaxList(
        XNamespace ns,
        IReadOnlyList<ManagedPassenger> passengers)
    {
        var list = new XElement(ns + "PaxList");

        foreach (var pax in passengers)
        {
            list.Add(new XElement(ns + "Pax",
                new XElement(ns + "PaxID", pax.PassengerId),
                new XElement(ns + "PTC", pax.Type),
                new XElement(ns + "Individual",
                    new XElement(ns + "GivenName", pax.GivenName.ToUpperInvariant()),
                    new XElement(ns + "Surname", pax.Surname.ToUpperInvariant()))));
        }

        return list;
    }

    // ── SeatAvailabilityRS ────────────────────────────────────────────────────

    /// <summary>
    /// Builds an IATA NDC 21.3 SeatAvailabilityRS XML document.
    ///
    /// Structure:
    ///   Document                       — schema version 21.3
    ///   Response/SeatAvailability      — one Flight per flight segment
    ///     Flight/SegmentRefs           — SEG1 key
    ///     Flight/CabinList/Cabin       — one entry per cabin
    ///       Cabin/ColumnList           — column letters with NDC position code
    ///       Cabin/RowList/Row          — row number + exit-row flag
    ///         Row/Seat                 — seat number, column, occupation status
    ///           Seat/OfferRef          — seat offer ID and unit price (when priced)
    ///   Response/DataLists             — FlightSegmentList, OriginDestinationList
    ///
    /// Occupation status: F=Free/available, O=Occupied, X=Blocked.
    /// Seat offers are joined to the layout by SeatNumber; seats absent from the
    /// offers list are treated as occupied/unavailable.
    /// </summary>
    public static string BuildSeatAvailabilityRS(NdcSeatAvailabilityResult result)
    {
        XNamespace ns = SeatAvailabilityRsNsUri;

        const string segKey = "SEG1";
        const string odKey  = "OD1";

        var seatOfferMap = result.SeatOffers?.SeatOffers
            .ToDictionary(o => o.SeatNumber, o => o, StringComparer.OrdinalIgnoreCase)
            ?? [];

        var root = new XElement(ns + "IATA_SeatAvailabilityRS",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            BuildSaDocument(ns),
            BuildSaResponse(ns, result, segKey, odKey, seatOfferMap));

        return SerialiseXml(root);
    }

    private static XElement BuildSaDocument(XNamespace ns) =>
        new(ns + "Document",
            new XElement(ns + "ReferenceVersion", "21.3"));

    private static XElement BuildSaResponse(
        XNamespace ns,
        NdcSeatAvailabilityResult result,
        string segKey,
        string odKey,
        Dictionary<string, SeatOfferDto> seatOfferMap)
    {
        var offer = result.OfferDetail!;
        var seatmap = result.Seatmap!;

        var flightEl = new XElement(ns + "Flight",
            new XElement(ns + "SegmentRefs", segKey),
            BuildSaCabinList(ns, seatmap, seatOfferMap));

        return new XElement(ns + "Response",
            new XElement(ns + "SeatAvailability",
                new XElement(ns + "Flights", flightEl)),
            BuildSaDataLists(ns, offer, segKey, odKey));
    }

    private static XElement BuildSaCabinList(
        XNamespace ns,
        SeatmapLayoutDto seatmap,
        Dictionary<string, SeatOfferDto> seatOfferMap)
    {
        var cabinList = new XElement(ns + "CabinList");

        foreach (var cabin in seatmap.Cabins)
        {
            // Build a per-column position lookup from the cabin's seats.
            var columnPosition = BuildColumnPositionMap(cabin);

            var cabinEl = new XElement(ns + "Cabin",
                new XElement(ns + "CabinCode", MapCabinToNdc(cabin.CabinCode)),
                new XElement(ns + "CabinName", cabin.CabinName),
                new XElement(ns + "DeckCode", cabin.DeckLevel ?? "Main"),
                new XElement(ns + "FirstRowNumber", cabin.StartRow.ToString()),
                new XElement(ns + "LastRowNumber", cabin.EndRow.ToString()),
                BuildSaColumnList(ns, cabin.Columns, columnPosition),
                BuildSaRowList(ns, cabin.Rows, seatOfferMap));

            cabinList.Add(cabinEl);
        }

        return cabinList;
    }

    private static XElement BuildSaColumnList(
        XNamespace ns,
        List<string> columns,
        Dictionary<string, string> columnPosition)
    {
        var colList = new XElement(ns + "ColumnList");

        foreach (var col in columns)
        {
            var colEl = new XElement(ns + "Column",
                new XElement(ns + "Position", col));

            columnPosition.TryGetValue(col, out var charCode);
            if (!string.IsNullOrWhiteSpace(charCode))
                colEl.Add(new XElement(ns + "SeatCharacteristicCode", charCode));

            colList.Add(colEl);
        }

        return colList;
    }

    private static XElement BuildSaRowList(
        XNamespace ns,
        List<RowLayoutDto> rows,
        Dictionary<string, SeatOfferDto> seatOfferMap)
    {
        var rowList = new XElement(ns + "RowList");

        foreach (var row in rows)
        {
            var isExitRow = row.Seats.Any(s =>
                s.Attributes.Any(a => a.Contains("EXIT", StringComparison.OrdinalIgnoreCase)));

            var rowEl = new XElement(ns + "Row",
                new XElement(ns + "RowNumber", row.RowNumber.ToString()));

            if (isExitRow)
                rowEl.Add(new XElement(ns + "CharacteristicCode", "K"));

            foreach (var seat in row.Seats)
                rowEl.Add(BuildSaSeat(ns, seat, seatOfferMap));

            rowList.Add(rowEl);
        }

        return rowList;
    }

    private static XElement BuildSaSeat(
        XNamespace ns,
        SeatLayoutDto seat,
        Dictionary<string, SeatOfferDto> seatOfferMap)
    {
        seatOfferMap.TryGetValue(seat.SeatNumber, out var offer);

        // Determine occupation status.
        // A seat is Free only when it appears in the offers list and is selectable.
        var statusCode = (offer is not null && offer.IsSelectable) ? "F"
                       : seat.IsSelectable                         ? "F"
                                                                   : "O";

        var seatEl = new XElement(ns + "Seat",
            new XElement(ns + "Column", seat.Column),
            new XElement(ns + "SeatNumber", seat.SeatNumber),
            new XElement(ns + "OccupationStatusCode", statusCode));

        // Seat characteristics (window, aisle, middle, exit).
        var charCode = MapSeatPositionToNdc(seat.Position);
        if (!string.IsNullOrWhiteSpace(charCode))
            seatEl.Add(new XElement(ns + "SeatCharacteristicCode", charCode));

        // Offer reference and pricing — only when a priced offer exists.
        if (offer is not null && offer.IsSelectable)
        {
            var currency = string.IsNullOrWhiteSpace(offer.CurrencyCode) ? "GBP" : offer.CurrencyCode;

            seatEl.Add(new XElement(ns + "OfferRef",
                new XElement(ns + "OfferRefID", offer.SeatOfferId),
                new XElement(ns + "UnitPrice",
                    new XElement(ns + "TotalAmount",
                        new XAttribute("CurCode", currency),
                        (offer.Price + offer.Tax).ToString("F2")),
                    new XElement(ns + "BaseAmount",
                        new XAttribute("CurCode", currency),
                        offer.Price.ToString("F2")),
                    new XElement(ns + "Taxes",
                        new XElement(ns + "Total",
                            new XAttribute("CurCode", currency),
                            offer.Tax.ToString("F2"))))));
        }

        return seatEl;
    }

    private static XElement BuildSaDataLists(
        XNamespace ns,
        OfferDetailDto offer,
        string segKey,
        string odKey)
    {
        DateOnly.TryParse(offer.DepartureDate, out var depDate);
        var arrDate = depDate.AddDays(offer.ArrivalDayOffset);
        var (airlineId, flightNum) = SplitFlightNumber(offer.FlightNumber);
        var aircraftCode = MapAircraftType(offer.AircraftType);

        var flightSegment = new XElement(ns + "FlightSegment",
            new XElement(ns + "SegmentKey", segKey),
            new XElement(ns + "Departure",
                new XElement(ns + "AirportCode", offer.Origin),
                new XElement(ns + "Date", offer.DepartureDate),
                new XElement(ns + "Time", offer.DepartureTime)),
            new XElement(ns + "Arrival",
                new XElement(ns + "AirportCode", offer.Destination),
                new XElement(ns + "Date", arrDate.ToString("yyyy-MM-dd")),
                new XElement(ns + "Time", offer.ArrivalTime)),
            new XElement(ns + "MarketingCarrier",
                new XElement(ns + "AirlineID", airlineId),
                new XElement(ns + "FlightNumber", flightNum),
                new XElement(ns + "Name", CarrierName)),
            new XElement(ns + "OperatingCarrier",
                new XElement(ns + "AirlineID", airlineId),
                new XElement(ns + "FlightNumber", flightNum)),
            new XElement(ns + "Equipment",
                new XElement(ns + "AircraftCode", aircraftCode)));

        var originDest = new XElement(ns + "OriginDestination",
            new XElement(ns + "OriginDestinationKey", odKey),
            new XElement(ns + "DepartureCode", offer.Origin),
            new XElement(ns + "ArrivalCode", offer.Destination),
            new XElement(ns + "FlightReferences", segKey));

        return new XElement(ns + "DataLists",
            new XElement(ns + "FlightSegmentList", flightSegment),
            new XElement(ns + "OriginDestinationList", originDest));
    }

    /// <summary>
    /// Builds a column → NDC position code map by inspecting the first seat found
    /// in each column across all rows of the cabin.
    /// </summary>
    private static Dictionary<string, string> BuildColumnPositionMap(CabinLayoutDto cabin)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in cabin.Rows)
        {
            foreach (var seat in row.Seats)
            {
                if (!map.ContainsKey(seat.Column))
                {
                    var code = MapSeatPositionToNdc(seat.Position);
                    if (!string.IsNullOrWhiteSpace(code))
                        map[seat.Column] = code;
                }
            }

            // Stop once all columns have been mapped.
            if (map.Count >= cabin.Columns.Count) break;
        }

        return map;
    }

    /// <summary>
    /// Maps internal seat position descriptors to NDC SeatCharacteristicCode values.
    /// NDC: W=Window, A=Aisle, M=Middle, K=ExitRow.
    /// </summary>
    private static string MapSeatPositionToNdc(string position)
    {
        if (string.IsNullOrWhiteSpace(position)) return string.Empty;

        return position.ToUpperInvariant() switch
        {
            var p when p.Contains("WINDOW") => "W",
            var p when p.Contains("AISLE")  => "A",
            var p when p.Contains("MIDDLE") => "M",
            var p when p.Contains("EXIT")   => "K",
            _                               => string.Empty
        };
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
