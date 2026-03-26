using ReservationSystem.Microservices.Schedule.Domain.Entities;

namespace ReservationSystem.Microservices.Schedule.Application.Ssim;

/// <summary>
/// Generates an IATA SSIM Chapter 7 file from a collection of flight schedules.
///
/// SSIM is a fixed-width, positional ASCII flat file. Every record is exactly
/// 200 characters wide (space-padded), terminated with CRLF.
///
/// Record types used:
///   1 — Transmission header
///   2 — Carrier header
///   3 — Flight leg record (one per schedule)
///   5 — Trailer (record count)
///
/// Times in Type 3 records are local with an embedded UTC offset of +00,
/// matching the convention that schedule times are stored as local at the
/// origin airport.  Consumers must apply the true UTC offset for each airport
/// when converting to UTC for operational use.
///
/// Day-of-week encoding: a 7-character string where each position corresponds
/// to a fixed day (1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat, 7=Sun). An
/// operating day carries its digit; a non-operating day is a space.
/// Example: daily = "1234567"; Mon/Wed/Fri = "1 3 5  ".
/// </summary>
public static class SsimGenerator
{
    private const string CarrierCode = "AX";
    private const int RecordWidth = 200;

    /// <summary>
    /// Builds a complete SSIM file string from the supplied schedules.
    /// The returned string uses CRLF line endings and is padded to 200 chars per record.
    /// </summary>
    public static string Generate(IReadOnlyList<FlightSchedule> schedules, DateOnly fileDate)
    {
        if (schedules.Count == 0)
            return BuildRecord1(fileDate, DateOnly.MinValue, DateOnly.MaxValue)
                 + BuildRecord2(fileDate, DateOnly.MinValue, DateOnly.MaxValue)
                 + BuildTrailer(0);

        var seasonStart = schedules.Min(s => DateOnly.FromDateTime(s.ValidFrom));
        var seasonEnd   = schedules.Max(s => DateOnly.FromDateTime(s.ValidTo));

        var sb = new System.Text.StringBuilder();
        sb.Append(BuildRecord1(fileDate, seasonStart, seasonEnd));
        sb.Append(BuildRecord2(fileDate, seasonStart, seasonEnd));

        int legCount = 0;
        foreach (var schedule in schedules.OrderBy(s => s.FlightNumber))
        {
            sb.Append(BuildRecord3(schedule));
            legCount++;
        }

        sb.Append(BuildTrailer(legCount));
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Type 1 — Transmission header
    // Format: 1[standard(4)][sender(3)][space][receiver(3)][space][route(6)][space]
    //         [seasonStart(8)][space][seasonEnd(8)][carrier(3)][space][fileType(5)][space]
    //         [fileDate(8)]  ... padded to 200
    // -------------------------------------------------------------------------
    private static string BuildRecord1(DateOnly fileDate, DateOnly seasonStart, DateOnly seasonEnd)
    {
        var raw = $"1IATA  {CarrierCode}  {"".PadRight(6)} {FormatDate(seasonStart)} {FormatDate(seasonEnd)}{CarrierCode}  SCHED  {FormatDate(fileDate)}";
        return Pad(raw) + "\r\n";
    }

    // -------------------------------------------------------------------------
    // Type 2 — Carrier header
    // Format: 2[carrier(3)][seq(3)][season(1)][seasonStart(8)][space][seasonEnd(8)]
    //         ... padded to 200
    // IATA season designator: W = Northern winter (Oct–Mar), E = Northern summer (Apr–Sep)
    // -------------------------------------------------------------------------
    private static string BuildRecord2(DateOnly fileDate, DateOnly seasonStart, DateOnly seasonEnd)
    {
        var season = (fileDate.Month >= 4 && fileDate.Month <= 9) ? "E" : "W";
        var raw = $"2{CarrierCode}  01{season}{FormatDate(seasonStart)} {FormatDate(seasonEnd)}";
        return Pad(raw) + "\r\n";
    }

    // -------------------------------------------------------------------------
    // Type 3 — Flight leg record
    //
    // Positional layout (1-based):
    //  1      Record type '3'
    //  2      Operational suffix (space = active record)
    //  3-4    Airline designator
    //  5      Space
    //  6-9    Flight number, zero-padded to 4 digits
    //  10     Space
    //  11     Service type: Y = scheduled passenger
    //  12     Space
    //  13-20  Period start YYYYMMDD
    //  21     Space
    //  22-29  Period end YYYYMMDD
    //  30-31  Two spaces (itinerary variation / leg sequence, unused for non-stop)
    //  32-38  Days of week (7 chars, positional: 1=Mon … 7=Sun, space = not operating)
    //  39     Space
    //  40-42  Departure station (IATA 3-char)
    //  43-46  Departure time local HHMM
    //  47-49  Departure UTC offset (+HH or -HH, space-padded to 3 chars; +00 used here)
    //  50-53  Arrival time local HHMM
    //  54     Arrival day offset (0 = same day, 1 = next day at destination)
    //  55     Space
    //  56-58  Destination station (IATA 3-char)
    //  59-61  Space
    //  62-64  Equipment / aircraft IATA type code (3-4 chars, left-justified)
    //  65     Space
    //  66-67  Operating carrier code
    //         ... remainder padded to 200
    // -------------------------------------------------------------------------
    private static string BuildRecord3(FlightSchedule schedule)
    {
        var flightNum = ExtractNumericFlightNumber(schedule.FlightNumber).PadLeft(4, '0');
        var periodStart = FormatDate(DateOnly.FromDateTime(schedule.ValidFrom));
        var periodEnd   = FormatDate(DateOnly.FromDateTime(schedule.ValidTo));
        var daysOfWeek  = BuildDaysOfWeek(schedule.DaysOfWeek);
        var depTime     = FormatTime(schedule.DepartureTime);
        var arrTime     = FormatTime(schedule.ArrivalTime);
        var dayOffset   = schedule.ArrivalDayOffset.ToString();
        var equipment   = schedule.AircraftType.Length > 3
            ? schedule.AircraftType[..3]   // SSIM equipment field is 3 chars in the core layout
            : schedule.AircraftType.PadRight(3);

        // Build the record field by field to match the positional layout above.
        // Each segment is explicit so the column positions are easy to audit.
        var raw =
            "3"           +   // pos 1:  record type
            " "           +   // pos 2:  operational suffix (space = active)
            CarrierCode   +   // pos 3-4: airline designator
            " "           +   // pos 5
            flightNum     +   // pos 6-9: flight number
            " "           +   // pos 10
            "Y"           +   // pos 11: service type
            " "           +   // pos 12
            periodStart   +   // pos 13-20
            " "           +   // pos 21
            periodEnd     +   // pos 22-29
            "  "          +   // pos 30-31: itinerary variation (unused)
            daysOfWeek    +   // pos 32-38
            " "           +   // pos 39
            schedule.Origin      +   // pos 40-42
            depTime       +   // pos 43-46
            "+00"         +   // pos 47-49: UTC offset (+00 — consumer applies real offset)
            arrTime       +   // pos 50-53
            dayOffset     +   // pos 54: arrival day offset
            " "           +   // pos 55
            schedule.Destination +   // pos 56-58
            "   "         +   // pos 59-61
            equipment     +   // pos 62-64
            " "           +   // pos 65
            CarrierCode;       // pos 66-67: operating carrier

        return Pad(raw) + "\r\n";
    }

    // -------------------------------------------------------------------------
    // Type 5 — Trailer
    // -------------------------------------------------------------------------
    private static string BuildTrailer(int legCount)
    {
        var raw = $"5{legCount,8}";
        return Pad(raw) + "\r\n";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Formats a date as YYYYMMDD.</summary>
    private static string FormatDate(DateOnly date) =>
        date == DateOnly.MinValue || date == DateOnly.MaxValue
            ? "        "
            : date.ToString("yyyyMMdd");

    /// <summary>Formats a TimeSpan as HHMM.</summary>
    private static string FormatTime(TimeSpan time) =>
        $"{time.Hours:D2}{time.Minutes:D2}";

    /// <summary>
    /// Converts the DaysOfWeek bitmask to the SSIM 7-character positional string.
    /// Bit positions: Mon=1(bit0), Tue=2(bit1), Wed=4(bit2), Thu=8(bit3),
    ///                Fri=16(bit4), Sat=32(bit5), Sun=64(bit6).
    /// SSIM positions: 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat, 7=Sun.
    /// An operating day shows its digit; non-operating days are space.
    /// </summary>
    private static string BuildDaysOfWeek(byte mask)
    {
        Span<char> chars = stackalloc char[7];
        for (int i = 0; i < 7; i++)
        {
            chars[i] = ((mask >> i) & 1) == 1 ? (char)('1' + i) : ' ';
        }
        return new string(chars);
    }

    /// <summary>
    /// Extracts the numeric suffix from a flight number such as "AX001" → "001".
    /// Falls back to the raw string if no digits can be extracted.
    /// </summary>
    private static string ExtractNumericFlightNumber(string flightNumber)
    {
        var digits = new string(flightNumber.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? flightNumber : digits;
    }

    /// <summary>Pads or truncates a record string to exactly <see cref="RecordWidth"/> characters.</summary>
    private static string Pad(string record) =>
        record.Length >= RecordWidth
            ? record[..RecordWidth]
            : record.PadRight(RecordWidth);
}
