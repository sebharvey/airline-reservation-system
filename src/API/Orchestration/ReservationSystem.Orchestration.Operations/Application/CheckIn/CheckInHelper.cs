using System.Text.Json;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.CheckIn;

/// <summary>
/// Shared passenger info parsed from order data — includes travel document fields for OLCI;
/// admin check-in only uses GivenName/Surname.
/// </summary>
public sealed record PaxInfo(
    string GivenName,
    string Surname,
    string? Dob,
    string? DocNationality,
    string? DocNumber,
    string? DocIssuingCountry,
    string? DocExpiryDate);

public static class CheckInHelper
{
    /// <summary>
    /// Parses order JSON to build ticket→passengerId and passengerId→passenger-info lookup maps.
    /// Reads dataLists.passengers (name + first travel doc) and eTickets.
    /// </summary>
    public static (Dictionary<string, string> TicketToPaxId, Dictionary<string, PaxInfo> PaxIdToInfo)
        ParseOrderLookups(JsonElement? orderData)
    {
        var ticketToPaxId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var paxIdToInfo = new Dictionary<string, PaxInfo>(StringComparer.OrdinalIgnoreCase);

        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return (ticketToPaxId, paxIdToInfo);

        if (el.TryGetProperty("dataLists", out var dl) &&
            dl.TryGetProperty("passengers", out var paxArr) &&
            paxArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var pax in paxArr.EnumerateArray())
            {
                var pid = pax.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() : null;
                if (pid is null) continue;

                var gn  = pax.TryGetProperty("givenName", out var gnEl) ? gnEl.GetString() ?? "" : "";
                var sn  = pax.TryGetProperty("surname",   out var snEl) ? snEl.GetString() ?? "" : "";
                var dob = pax.TryGetProperty("dob",       out var dobEl) ? dobEl.GetString() : null;

                string? docNationality = null, docNumber = null, docIssuingCountry = null, docExpiryDate = null;
                if (pax.TryGetProperty("docs", out var docs) &&
                    docs.ValueKind == JsonValueKind.Array &&
                    docs.GetArrayLength() > 0)
                {
                    var doc = docs[0];
                    docNationality   = doc.TryGetProperty("nationality",    out var nat) ? nat.GetString() : null;
                    docNumber        = doc.TryGetProperty("number",         out var num) ? num.GetString() : null;
                    docIssuingCountry = doc.TryGetProperty("issuingCountry", out var ic)  ? ic.GetString()  : null;
                    docExpiryDate    = doc.TryGetProperty("expiryDate",     out var ed)  ? ed.GetString()  : null;
                }

                paxIdToInfo[pid] = new PaxInfo(gn, sn, dob, docNationality, docNumber, docIssuingCountry, docExpiryDate);
            }
        }

        if (el.TryGetProperty("eTickets", out var eTickets) &&
            eTickets.ValueKind == JsonValueKind.Array)
        {
            foreach (var et in eTickets.EnumerateArray())
            {
                var ticketNum = et.TryGetProperty("eTicketNumber", out var tnEl) ? tnEl.GetString() : null;
                var paxId     = et.TryGetProperty("passengerId",   out var pidEl) ? pidEl.GetString() : null;
                if (ticketNum is not null && paxId is not null)
                    ticketToPaxId[ticketNum] = paxId;
            }
        }

        return (ticketToPaxId, paxIdToInfo);
    }

    /// <summary>
    /// Converts Timatic check results into OrderTimaticNote entries for order audit persistence.
    /// </summary>
    public static List<OrderTimaticNote> BuildTimaticNotes(
        IReadOnlyList<OciTimaticNote> notes,
        IReadOnlyDictionary<string, string>? ticketToName = null,
        IReadOnlyDictionary<string, string>? ticketToPaxId = null)
        => notes.Select(n =>
        {
            var checkLabel = n.CheckType switch
            {
                "DOC"  => "Document check",
                "APIS" => "APIS check",
                _      => $"{n.CheckType} check"
            };
            var isFail     = !string.Equals(n.Status, "PASS", StringComparison.OrdinalIgnoreCase);
            var statusText = isFail ? "failed" : "passed";
            var paxName = isFail
                && ticketToName is not null
                && ticketToName.TryGetValue(n.TicketNumber, out var name)
                && !string.IsNullOrWhiteSpace(name)
                    ? name : null;
            var subject = paxName is not null
                ? $"{paxName} (ticket {n.TicketNumber})"
                : $"ticket {n.TicketNumber}";
            var message = string.IsNullOrWhiteSpace(n.Detail)
                ? $"{checkLabel} {statusText} for {subject}"
                : $"{checkLabel} {statusText} for {subject}: {n.Detail}";
            var paxIdStr = ticketToPaxId is not null
                && ticketToPaxId.TryGetValue(n.TicketNumber, out var pid) ? pid : null;
            return new OrderTimaticNote
            {
                DateTime = n.Timestamp,
                Type     = "OCI",
                Message  = message,
                PaxId    = ExtractPaxIdInt(paxIdStr)
            };
        }).ToList();

    /// <summary>
    /// Parses order JSON to build passengerId→eTicketNumber lookup (reverse of ParseOrderLookups).
    /// Used by the OLCI retrieve step to map passengers to their ticket numbers.
    /// </summary>
    public static Dictionary<string, string> ParsePaxToTicketMap(JsonElement? orderData)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (orderData is not JsonElement el || el.ValueKind != JsonValueKind.Object) return map;
        if (!el.TryGetProperty("eTickets", out var eTickets) || eTickets.ValueKind != JsonValueKind.Array) return map;
        foreach (var et in eTickets.EnumerateArray())
        {
            var paxId     = et.TryGetProperty("passengerId",   out var pEl) ? pEl.GetString() : null;
            var ticketNum = et.TryGetProperty("eTicketNumber", out var tEl) ? tEl.GetString() : null;
            if (paxId is not null && ticketNum is not null)
                map[paxId] = ticketNum;
        }
        return map;
    }

    /// <summary>
    /// Converts watchlist match results into OrderTimaticNote entries for order audit persistence.
    /// </summary>
    public static List<OrderTimaticNote> BuildWatchlistNotes(IReadOnlyList<WatchlistMatch> matches)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        return matches.Select(m =>
        {
            var name = $"{m.GivenName} {m.Surname}".Trim();
            var subject = name.Length > 0
                ? $"{name} (ticket {m.TicketNumber})"
                : $"ticket {m.TicketNumber}";
            var detail = string.IsNullOrWhiteSpace(m.Notes)
                ? $"Passenger {subject} matched security watchlist entry for passport {m.PassportNumber}"
                : $"Passenger {subject} matched security watchlist entry for passport {m.PassportNumber}: {m.Notes}";
            return new OrderTimaticNote
            {
                DateTime = timestamp,
                Type     = "OCI",
                Message  = detail,
                PaxId    = ExtractPaxIdInt(m.PassengerId)
            };
        }).ToList();
    }

    /// <summary>
    /// Extracts the integer suffix from a composite passenger ID such as "PAX-123".
    /// </summary>
    public static int? ExtractPaxIdInt(string? paxId)
    {
        if (string.IsNullOrEmpty(paxId)) return null;
        var dash = paxId.LastIndexOf('-');
        return dash >= 0 && int.TryParse(paxId[(dash + 1)..], out var n) ? n : null;
    }
}
