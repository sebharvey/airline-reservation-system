using System.Xml.Linq;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderRetrieve;
using ReservationSystem.Orchestration.Retail.Application.NdcSeatAvailability;
using ReservationSystem.Orchestration.Retail.Application.NdcServiceList;

namespace ReservationSystem.Orchestration.Retail.Models.Ndc;

/// <summary>
/// Parses IATA NDC 21.3 request XML documents into internal command records.
/// The parser accepts any NDC namespace version; it resolves the namespace from the
/// root element and applies it consistently throughout.
/// </summary>
public static class NdcXmlParser
{
    public static NdcAirShoppingCommand? TryParse(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return null;
        }

        // Resolve namespace from the root element so any NDC version is accepted.
        var ns = root.Name.Namespace;

        // ── CoreQuery / OriginDestination ─────────────────────────────────────
        var coreQuery = root.Element(ns + "CoreQuery");
        var od = coreQuery
            ?.Element(ns + "OriginDestinations")
            ?.Element(ns + "OriginDestination");

        if (od is null)
        {
            errorMessage = "CoreQuery/OriginDestinations/OriginDestination element is missing.";
            return null;
        }

        var departure = od.Element(ns + "Departure");
        var arrival = od.Element(ns + "Arrival");

        var origin = departure?.Element(ns + "AirportCode")?.Value?.Trim().ToUpperInvariant();
        var destination = arrival?.Element(ns + "AirportCode")?.Value?.Trim().ToUpperInvariant();
        var departureDateStr = departure?.Element(ns + "Date")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(origin) || origin.Length != 3)
        {
            errorMessage = "Departure/AirportCode is missing or not a 3-letter IATA code.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(destination) || destination.Length != 3)
        {
            errorMessage = "Arrival/AirportCode is missing or not a 3-letter IATA code.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(departureDateStr) || !DateOnly.TryParse(departureDateStr, out var parsedDate))
        {
            errorMessage = "Departure/Date is missing or not in yyyy-MM-dd format.";
            return null;
        }

        // ── Travelers ─────────────────────────────────────────────────────────
        var paxList = new List<NdcPassengerType>();
        var travelers = root.Element(ns + "Travelers");

        if (travelers is not null)
        {
            foreach (var traveler in travelers.Elements(ns + "Traveler"))
            {
                var anon = traveler.Element(ns + "AnonymousTraveler");
                if (anon is null) continue;

                var ptcRaw = anon.Element(ns + "PTC")?.Value?.Trim().ToUpperInvariant();
                var ptc = ptcRaw switch
                {
                    "ADT" or "CHD" or "INF" or "YTH" => ptcRaw,
                    _ when !string.IsNullOrWhiteSpace(ptcRaw) => ptcRaw,
                    _ => "ADT"
                };

                var quantityStr = anon.Element(ns + "Quantity")?.Value?.Trim() ?? "1";
                var quantity = int.TryParse(quantityStr, out var q) && q > 0 ? q : 1;

                paxList.Add(new NdcPassengerType(ptc, quantity));
            }
        }

        // Default to 1 adult if no travelers specified.
        if (paxList.Count == 0)
            paxList.Add(new NdcPassengerType("ADT", 1));

        var totalPax = paxList.Sum(p => p.Quantity);
        if (totalPax < 1)
        {
            errorMessage = "Total passenger count must be at least 1.";
            return null;
        }

        errorMessage = null;
        return new NdcAirShoppingCommand(
            origin,
            destination,
            parsedDate.ToString("yyyy-MM-dd"),
            totalPax,
            paxList);
    }

    // ── OfferPrice parser ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses an IATA_OfferPriceRQ XML document.
    /// Extracts SelectedOffer/OfferRefID (must be a valid GUID), optional
    /// SelectedOffer/OfferItemRef/OfferItemRefID, optional ShoppingResponseID/ResponseID,
    /// and optional Travelers (same structure as AirShoppingRQ).
    /// </summary>
    public static NdcOfferPriceCommand? TryParseOfferPriceRq(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return null;
        }

        var ns = root.Name.Namespace;

        // ── SelectedOffer / OfferRefID ─────────────────────────────────────────
        var selectedOffer = root.Element(ns + "SelectedOffer");
        if (selectedOffer is null)
        {
            errorMessage = "SelectedOffer element is missing.";
            return null;
        }

        var offerRefIdStr = selectedOffer.Element(ns + "OfferRefID")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(offerRefIdStr) || !Guid.TryParse(offerRefIdStr, out var offerRefId))
        {
            errorMessage = "SelectedOffer/OfferRefID is missing or not a valid GUID.";
            return null;
        }

        var offerItemRefId = selectedOffer
            .Element(ns + "OfferItemRef")
            ?.Element(ns + "OfferItemRefID")?.Value?.Trim();

        // ── ShoppingResponseID ────────────────────────────────────────────────
        var shoppingResponseId = root
            .Element(ns + "ShoppingResponseID")
            ?.Element(ns + "ResponseID")?.Value?.Trim();

        // ── Travelers (optional) ──────────────────────────────────────────────
        var paxList = new List<NdcPassengerType>();
        var travelers = root.Element(ns + "Travelers");
        if (travelers is not null)
        {
            foreach (var traveler in travelers.Elements(ns + "Traveler"))
            {
                var anon = traveler.Element(ns + "AnonymousTraveler");
                if (anon is null) continue;

                var ptcRaw = anon.Element(ns + "PTC")?.Value?.Trim().ToUpperInvariant();
                var ptc = !string.IsNullOrWhiteSpace(ptcRaw) ? ptcRaw : "ADT";

                var quantityStr = anon.Element(ns + "Quantity")?.Value?.Trim() ?? "1";
                var quantity = int.TryParse(quantityStr, out var q) && q > 0 ? q : 1;

                paxList.Add(new NdcPassengerType(ptc, quantity));
            }
        }

        errorMessage = null;
        return new NdcOfferPriceCommand(
            offerRefId,
            offerItemRefId,
            shoppingResponseId,
            paxList.Count > 0 ? paxList : null);
    }

    // ── OrderCreate parser ────────────────────────────────────────────────────

    /// <summary>
    /// Parses an IATA_OrderCreateRQ XML document (NDC 21.3).
    ///
    /// Extracts:
    ///   Query/OrderItems/OfferItem/OfferRefID            — stored offer GUID (required)
    ///   Query/OrderItems/OfferItem/OfferItemRefID        — offer item ref (optional)
    ///   Query/OrderItems/ShoppingResponse/ResponseID    — shopping correlation ID (optional)
    ///   Query/DataLists/PaxList/Pax                     — named passengers (at least 1 required)
    ///   Query/DataLists/ContactInfoList/ContactInfo     — contact info indexed by ContactInfoID
    ///   Query/Payments/Payment/Method/PaymentCard       — card payment details (optional)
    /// </summary>
    public static NdcOrderCreateCommand? TryParseOrderCreateRq(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return null;
        }

        var ns = root.Name.Namespace;

        // ── Query ─────────────────────────────────────────────────────────────
        var query = root.Element(ns + "Query");
        if (query is null)
        {
            errorMessage = "Query element is missing.";
            return null;
        }

        // ── OfferItem / OfferRefID ─────────────────────────────────────────────
        var orderItems = query.Element(ns + "OrderItems");
        if (orderItems is null)
        {
            errorMessage = "Query/OrderItems element is missing.";
            return null;
        }

        var offerItemEl = orderItems.Element(ns + "OfferItem");
        if (offerItemEl is null)
        {
            errorMessage = "Query/OrderItems/OfferItem element is missing.";
            return null;
        }

        var offerRefIdStr = offerItemEl.Element(ns + "OfferRefID")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(offerRefIdStr) || !Guid.TryParse(offerRefIdStr, out var offerRefId))
        {
            errorMessage = "Query/OrderItems/OfferItem/OfferRefID is missing or not a valid GUID.";
            return null;
        }

        var offerItemRefId = offerItemEl.Element(ns + "OfferItemRefID")?.Value?.Trim();

        // ── ShoppingResponseID ────────────────────────────────────────────────
        var shoppingResponseId = orderItems
            .Element(ns + "ShoppingResponse")
            ?.Element(ns + "ResponseID")?.Value?.Trim();

        // ── DataLists ─────────────────────────────────────────────────────────
        var dataLists = query.Element(ns + "DataLists");

        // Build contact info lookup keyed by ContactInfoID.
        var contactMap = BuildContactMap(ns, dataLists);

        // ── Passengers ────────────────────────────────────────────────────────
        var passengers = new List<NdcOrderCreatePassenger>();
        var paxListEl = dataLists?.Element(ns + "PaxList");

        if (paxListEl is null)
        {
            errorMessage = "Query/DataLists/PaxList element is missing.";
            return null;
        }

        foreach (var paxEl in paxListEl.Elements(ns + "Pax"))
        {
            var paxId = paxEl.Element(ns + "PaxID")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(paxId)) continue;

            var ptcRaw = paxEl.Element(ns + "PTC")?.Value?.Trim().ToUpperInvariant();
            var ptc = ptcRaw switch
            {
                "ADT" or "CHD" or "INF" or "YTH" => ptcRaw,
                _ when !string.IsNullOrWhiteSpace(ptcRaw) => ptcRaw,
                _ => "ADT"
            };

            var individual = paxEl.Element(ns + "Individual");
            var givenName  = individual?.Element(ns + "GivenName")?.Value?.Trim() ?? string.Empty;
            var surname    = individual?.Element(ns + "Surname")?.Value?.Trim() ?? string.Empty;
            var dob        = individual?.Element(ns + "Birthdate")?.Value?.Trim();
            var genderCode = individual?.Element(ns + "GenderCode")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(givenName) || string.IsNullOrWhiteSpace(surname))
            {
                errorMessage = $"Pax {paxId}: Individual/GivenName and Individual/Surname are required.";
                return null;
            }

            // Resolve contact info via ContactInfoRefID on the Pax element.
            string? email = null;
            string? phone = null;
            var contactRefId = paxEl.Element(ns + "ContactInfoRefID")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(contactRefId) &&
                contactMap.TryGetValue(contactRefId, out var contact))
            {
                email = contact.Email;
                phone = contact.Phone;
            }

            passengers.Add(new NdcOrderCreatePassenger(
                paxId, ptc, givenName, surname, dob, genderCode, email, phone));
        }

        if (passengers.Count == 0)
        {
            errorMessage = "At least one Pax must be specified in Query/DataLists/PaxList.";
            return null;
        }

        // ── GDS booking reference (optional) ─────────────────────────────────
        var gdsBookingReference = query
            .Element(ns + "BookingReferences")
            ?.Element(ns + "BookingReference")
            ?.Element(ns + "ID")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(gdsBookingReference))
            gdsBookingReference = null;

        // ── Payment ───────────────────────────────────────────────────────────
        NdcOrderCreatePaymentCard? paymentCard = null;
        var paymentsEl = query.Element(ns + "Payments");
        var paymentEl  = paymentsEl?.Element(ns + "Payment");

        if (paymentEl is not null)
        {
            var cardEl = paymentEl
                .Element(ns + "Method")
                ?.Element(ns + "PaymentCard");

            if (cardEl is not null)
            {
                var cardholderName   = cardEl.Element(ns + "CardHolderName")?.Value?.Trim() ?? string.Empty;
                var plainCardNumber  = cardEl.Element(ns + "CardNumber")
                    ?.Element(ns + "PlainCardNumber")?.Value?.Trim() ?? string.Empty;
                var cardTypeCode     = cardEl.Element(ns + "CardTypeCode")?.Value?.Trim() ?? string.Empty;
                var expiryEl         = cardEl.Element(ns + "Expiry");
                var expiryMonth      = expiryEl?.Element(ns + "Month")?.Value?.Trim() ?? string.Empty;
                var expiryYear       = expiryEl?.Element(ns + "Year")?.Value?.Trim() ?? string.Empty;
                var cvv              = cardEl.Element(ns + "SeriesCode")
                    ?.Element(ns + "Value")?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(plainCardNumber))
                {
                    paymentCard = new NdcOrderCreatePaymentCard(
                        cardholderName, plainCardNumber, cardTypeCode,
                        expiryMonth, expiryYear, cvv);
                }
            }
        }

        errorMessage = null;
        return new NdcOrderCreateCommand(
            offerRefId,
            string.IsNullOrWhiteSpace(offerItemRefId) ? null : offerItemRefId,
            string.IsNullOrWhiteSpace(shoppingResponseId) ? null : shoppingResponseId,
            passengers,
            paymentCard,
            gdsBookingReference);
    }

    // ── SeatAvailability parser ───────────────────────────────────────────────

    /// <summary>
    /// Parses an IATA_SeatAvailabilityRQ XML document (NDC 21.3).
    ///
    /// Extracts:
    ///   Query/OriginDestCriteria/OfferRefID   — stored offer GUID (required)
    ///   Travelers/Traveler/AnonymousTraveler  — passenger types (optional)
    ///
    /// The OfferRefID is used to resolve the flight's InventoryId and AircraftType
    /// from the stored offer before querying the seat map and seat offers.
    /// </summary>
    public static NdcSeatAvailabilityCommand? TryParseSeatAvailabilityRq(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return null;
        }

        var ns = root.Name.Namespace;

        // ── OfferRefID (required) ─────────────────────────────────────────────
        var offerRefIdStr = root
            .Element(ns + "Query")
            ?.Element(ns + "OriginDestCriteria")
            ?.Element(ns + "OfferRefID")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(offerRefIdStr) || !Guid.TryParse(offerRefIdStr, out var offerRefId))
        {
            errorMessage = "Query/OriginDestCriteria/OfferRefID is missing or not a valid GUID.";
            return null;
        }

        // ── Travelers (optional) ──────────────────────────────────────────────
        var paxList = new List<NdcPassengerType>();
        var travelers = root.Element(ns + "Travelers");
        if (travelers is not null)
        {
            foreach (var traveler in travelers.Elements(ns + "Traveler"))
            {
                var anon = traveler.Element(ns + "AnonymousTraveler");
                if (anon is null) continue;

                var ptcRaw = anon.Element(ns + "PTC")?.Value?.Trim().ToUpperInvariant();
                var ptc = !string.IsNullOrWhiteSpace(ptcRaw) ? ptcRaw : "ADT";

                var quantityStr = anon.Element(ns + "Quantity")?.Value?.Trim() ?? "1";
                var quantity = int.TryParse(quantityStr, out var q) && q > 0 ? q : 1;

                paxList.Add(new NdcPassengerType(ptc, quantity));
            }
        }

        errorMessage = null;
        return new NdcSeatAvailabilityCommand(
            offerRefId,
            paxList.Count > 0 ? paxList : null);
    }

    // ── OrderRetrieve parser ──────────────────────────────────────────────────

    /// <summary>
    /// Parses an IATA_OrderRetrieveRQ XML document (NDC 21.3).
    ///
    /// Extracts:
    ///   Query/Filters/OrderFilter/BookingRef/ID       — booking reference (required)
    ///   Query/Filters/OrderFilter/Pax/Individual/Surname — lead passenger surname (required)
    ///
    /// The surname is used for guest authentication; the Order microservice validates it
    /// against the lead passenger on the booking before returning order data.
    /// </summary>
    public static NdcOrderRetrieveCommand? TryParseOrderRetrieveRq(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return null;
        }

        var ns = root.Name.Namespace;

        var orderFilter = root
            .Element(ns + "Query")
            ?.Element(ns + "Filters")
            ?.Element(ns + "OrderFilter");

        if (orderFilter is null)
        {
            errorMessage = "Query/Filters/OrderFilter element is missing.";
            return null;
        }

        var bookingReference = orderFilter
            .Element(ns + "BookingRef")
            ?.Element(ns + "ID")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(bookingReference))
        {
            errorMessage = "Query/Filters/OrderFilter/BookingRef/ID is missing.";
            return null;
        }

        var surname = orderFilter
            .Element(ns + "Pax")
            ?.Element(ns + "Individual")
            ?.Element(ns + "Surname")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(surname))
        {
            errorMessage = "Query/Filters/OrderFilter/Pax/Individual/Surname is missing.";
            return null;
        }

        errorMessage = null;
        return new NdcOrderRetrieveCommand(bookingReference, surname);
    }

    // ── ServiceList parser ────────────────────────────────────────────────────

    /// <summary>
    /// Parses an IATA_ServiceListRQ XML document (NDC 21.3).
    ///
    /// Extracts:
    ///   Query/SelectionCriteria/OfferRef/OfferRefID  — stored offer GUID (optional)
    ///   Query/CabinPreferences/CabinType/CabinTypeCode/Code — NDC cabin code (optional)
    ///   Travelers/Traveler/AnonymousTraveler          — passenger types (optional)
    /// </summary>
    public static NdcServiceListCommand TryParseServiceListRq(string xml, out string? errorMessage)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid XML: {ex.Message}";
            return new NdcServiceListCommand(null, null, null);
        }

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Empty XML document.";
            return new NdcServiceListCommand(null, null, null);
        }

        var ns = root.Name.Namespace;

        // ── OfferRefID (optional) ─────────────────────────────────────────────
        Guid? offerRefId = null;
        var offerRefIdStr = root
            .Element(ns + "Query")
            ?.Element(ns + "SelectionCriteria")
            ?.Element(ns + "OfferRef")
            ?.Element(ns + "OfferRefID")?.Value?.Trim();

        if (!string.IsNullOrWhiteSpace(offerRefIdStr) && Guid.TryParse(offerRefIdStr, out var parsedId))
            offerRefId = parsedId;

        // ── CabinType (optional) ──────────────────────────────────────────────
        var ndcCabinCode = root
            .Element(ns + "Query")
            ?.Element(ns + "CabinPreferences")
            ?.Element(ns + "CabinType")
            ?.Element(ns + "CabinTypeCode")
            ?.Element(ns + "Code")?.Value?.Trim().ToUpperInvariant();

        // ── Travelers (optional) ──────────────────────────────────────────────
        var paxList = new List<NdcPassengerType>();
        var travelers = root.Element(ns + "Travelers");
        if (travelers is not null)
        {
            foreach (var traveler in travelers.Elements(ns + "Traveler"))
            {
                var anon = traveler.Element(ns + "AnonymousTraveler");
                if (anon is null) continue;

                var ptcRaw = anon.Element(ns + "PTC")?.Value?.Trim().ToUpperInvariant();
                var ptc = !string.IsNullOrWhiteSpace(ptcRaw) ? ptcRaw : "ADT";

                var quantityStr = anon.Element(ns + "Quantity")?.Value?.Trim() ?? "1";
                var quantity = int.TryParse(quantityStr, out var q) && q > 0 ? q : 1;

                paxList.Add(new NdcPassengerType(ptc, quantity));
            }
        }

        errorMessage = null;
        return new NdcServiceListCommand(
            offerRefId,
            string.IsNullOrWhiteSpace(ndcCabinCode) ? null : ndcCabinCode,
            paxList.Count > 0 ? paxList : null);
    }

    private static Dictionary<string, (string? Email, string? Phone)> BuildContactMap(
        XNamespace ns, XElement? dataLists)
    {
        var map = new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase);
        if (dataLists is null) return map;

        var contactInfoList = dataLists.Element(ns + "ContactInfoList");
        if (contactInfoList is null) return map;

        foreach (var ciEl in contactInfoList.Elements(ns + "ContactInfo"))
        {
            var ciId = ciEl.Element(ns + "ContactInfoID")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(ciId)) continue;

            var email = ciEl.Element(ns + "EmailAddress")
                ?.Element(ns + "EmailAddressText")?.Value?.Trim();
            var phone = ciEl.Element(ns + "Phone")
                ?.Element(ns + "PhoneNumber")?.Value?.Trim();

            map[ciId] = (email, phone);
        }

        return map;
    }
}
