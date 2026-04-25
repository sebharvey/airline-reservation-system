using System.Xml.Linq;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

namespace ReservationSystem.Orchestration.Retail.Models.Ndc;

/// <summary>
/// Parses an IATA NDC 21.3 AirShoppingRQ XML document into an NdcAirShoppingCommand.
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
}
