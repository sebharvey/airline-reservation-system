using System.Text;
using System.Xml;
using System.Xml.Linq;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Models.Ndc;

/// <summary>
/// Builds an IATA NDC 21.3 AirShoppingRS XML document from the internal offer search result.
///
/// Mapping strategy:
///   FlightItemDto        → one FlightSegment in DataLists/FlightSegmentList
///   FlightItemDto        → one OriginDestination in DataLists/OriginDestinationList
///   OfferItemDto         → one Offer/OfferItem in OffersGroup/AirlineOffers
///   NdcPassengerType[]   → AnonymousTraveler entries in DataLists/AnonymousTravelerList
///
/// OfferID in the NDC response equals the internal OfferId GUID so that NDC consumers
/// can reference it when initiating an order.
/// </summary>
public static class NdcXmlBuilder
{
    private const string NsUri = "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";
    private const string CarrierCode = "AX";
    private const string CarrierName = "Apex Air";

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

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);

        // Write to MemoryStream with UTF-8 (no BOM) to ensure the XML declaration
        // correctly declares encoding="UTF-8" and the output bytes are valid UTF-8.
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
