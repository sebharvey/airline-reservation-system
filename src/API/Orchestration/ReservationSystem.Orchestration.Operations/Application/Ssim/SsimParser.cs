namespace ReservationSystem.Orchestration.Operations.Application.Ssim;

/// <summary>
/// Parses an IATA SSIM Chapter 7 plain-text file and converts each Type 3
/// flight leg record into a <see cref="SsimFlightRecord"/>.
///
/// Field positions (0-indexed):
///  Index  Width  Field
///  0      1      Record type ('3' = active leg)
///  2      2      Airline IATA designator
///  5      4      Flight number (numeric, zero-padded to 4 digits)
///  10     1      Service type ('Y' = scheduled passenger)
///  12     8      Period start YYYYMMDD
///  21     8      Period end YYYYMMDD
///  31     7      Days of week (positional: position 1=Mon … 7=Sun; digit = operates, space = not)
///  39     3      Departure station (IATA)
///  42     4      Departure time local HHMM
///  46     3      Departure UTC offset (e.g. +00)
///  49     4      Arrival time local HHMM
///  53     1      Arrival day offset ('0' = same day, '1' = next day)
///  55     3      Destination station (IATA)
///  61     3      Equipment / aircraft type code
/// </summary>
public static class SsimParser
{
    /// <summary>
    /// Parses an SSIM file and returns a flight record for every valid Type 3 scheduled-passenger leg.
    /// </summary>
    public static SsimParseResult Parse(string ssimText, string createdBy)
    {
        var records = new List<SsimFlightRecord>();
        string? carrierCode = null;
        string? seasonCode = null;
        string? seasonStart = null;
        string? seasonEnd = null;

        foreach (var rawLine in ssimText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;

            switch (line[0])
            {
                case '2' when line.Length >= 14:
                    // Type 2: carrier header — extract airline designator, season code and validity.
                    carrierCode = line.Substring(1, 2).Trim();
                    seasonCode  = line.Substring(3, 3).Trim();
                    if (line.Length >= 22 && DateTime.TryParseExact(
                            line.Substring(5, 8), "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var sf))
                        seasonStart = sf.ToString("yyyy-MM-dd");
                    if (line.Length >= 22 && DateTime.TryParseExact(
                            line.Substring(14, 8), "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var st))
                        seasonEnd = st.ToString("yyyy-MM-dd");
                    break;

                case '3' when line.Length >= 68:
                    // Type 3: flight leg record.
                    if (line[10] != 'Y') continue; // Scheduled passenger only.
                    var record = TryParseType3(line, createdBy);
                    if (record is not null) records.Add(record);
                    break;
            }
        }

        return new SsimParseResult(
            CarrierCode: carrierCode ?? string.Empty,
            SeasonCode: seasonCode ?? string.Empty,
            SeasonStart: seasonStart ?? string.Empty,
            SeasonEnd: seasonEnd ?? string.Empty,
            Records: records.AsReadOnly());
    }

    private static SsimFlightRecord? TryParseType3(string line, string createdBy)
    {
        try
        {
            var carrier   = line.Substring(2, 2).Trim();
            var flightNum = BuildFlightNumber(carrier, line.Substring(5, 4));

            if (!DateTime.TryParseExact(line.Substring(12, 8), "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var validFrom))
                return null;

            if (!DateTime.TryParseExact(line.Substring(21, 8), "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var validTo))
                return null;

            var daysOfWeek = ParseDaysOfWeek(line.Substring(31, 7));
            if (daysOfWeek == 0) return null;

            var origin      = line.Substring(39, 3).Trim();
            var destination = line.Substring(55, 3).Trim();
            var departureTime    = ParseHhmm(line.Substring(42, 4));
            var arrivalTime      = ParseHhmm(line.Substring(49, 4));
            var arrivalDayOffset = line[53] == '1' ? (byte)1 : (byte)0;
            var aircraftType     = ResolveAircraftType(line.Substring(61, 3).Trim());

            var utcOffset = ParseUtcOffset(line.Substring(46, 3));
            var (depUtc, arrUtc, arrDayOffsetUtc) = ComputeUtcTimes(departureTime, arrivalTime, arrivalDayOffset, utcOffset);

            return new SsimFlightRecord(
                FlightNumber: flightNum,
                Origin: origin,
                Destination: destination,
                DepartureTime: departureTime.ToString(@"hh\:mm"),
                ArrivalTime: arrivalTime.ToString(@"hh\:mm"),
                ArrivalDayOffset: arrivalDayOffset,
                DaysOfWeek: daysOfWeek,
                AircraftType: aircraftType,
                ValidFrom: validFrom.ToString("yyyy-MM-dd"),
                ValidTo: validTo.ToString("yyyy-MM-dd"),
                CreatedBy: createdBy,
                DepartureTimeUtc: depUtc.ToString(@"hh\:mm"),
                ArrivalTimeUtc: arrUtc.ToString(@"hh\:mm"),
                ArrivalDayOffsetUtc: arrDayOffsetUtc);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildFlightNumber(string carrier, string ssimNumeric)
    {
        if (!int.TryParse(ssimNumeric, out var num))
            return carrier + ssimNumeric.TrimStart('0').PadLeft(3, '0');

        var digits = num.ToString();
        if (digits.Length < 3) digits = digits.PadLeft(3, '0');
        return carrier + digits;
    }

    private static byte ParseDaysOfWeek(string dowField)
    {
        byte mask = 0;
        for (var i = 0; i < 7 && i < dowField.Length; i++)
        {
            if (dowField[i] != ' ')
                mask |= (byte)(1 << i);
        }
        return mask;
    }

    private static TimeSpan ParseHhmm(string hhmm) =>
        new(int.Parse(hhmm.Substring(0, 2)), int.Parse(hhmm.Substring(2, 2)), 0);

    /// <summary>
    /// Parses a 3-character SSIM UTC offset string (e.g. "+00", "-05", "+10") into a TimeSpan.
    /// Positive offsets are east of UTC (local time is ahead of UTC).
    /// </summary>
    private static TimeSpan ParseUtcOffset(string s)
    {
        if (s.Length < 3 || (s[0] != '+' && s[0] != '-')) return TimeSpan.Zero;
        if (!int.TryParse(s.Substring(1, 2), out var hours)) return TimeSpan.Zero;
        return s[0] == '-' ? TimeSpan.FromHours(-hours) : TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// Computes UTC departure and arrival times from local times and the departure UTC offset.
    /// The arrival UTC time is derived by adding the local flight duration to the UTC departure,
    /// which is accurate regardless of the destination timezone.
    /// </summary>
    private static (TimeSpan DepUtc, TimeSpan ArrUtc, byte ArrDayOffsetUtc) ComputeUtcTimes(
        TimeSpan departureTime, TimeSpan arrivalTime, byte arrivalDayOffset, TimeSpan utcOffset)
    {
        // UTC departure = local departure - UTC offset (e.g. UTC+5: 10:00 local → 05:00 UTC)
        var depUtcRaw = departureTime - utcOffset;

        // Flight duration from local times; arrivalDayOffset accounts for overnight legs
        var flightDuration = arrivalTime - departureTime + TimeSpan.FromDays(arrivalDayOffset);
        if (flightDuration < TimeSpan.Zero) flightDuration += TimeSpan.FromDays(1);

        var arrUtcRaw = depUtcRaw + flightDuration;

        // Determine how many UTC days separate departure and arrival (typically 0 or 1)
        static int FloorDiv(double a, double b) => (int)Math.Floor(a / b);
        var arrDayOffsetUtc = (byte)Math.Max(0, FloorDiv(arrUtcRaw.TotalDays, 1) - FloorDiv(depUtcRaw.TotalDays, 1));

        // Normalise both times to [00:00, 23:59]
        static TimeSpan Normalise(TimeSpan t)
        {
            var mins = ((int)t.TotalMinutes % 1440 + 1440) % 1440;
            return TimeSpan.FromMinutes(mins);
        }

        return (Normalise(depUtcRaw), Normalise(arrUtcRaw), arrDayOffsetUtc);
    }

    private static string ResolveAircraftType(string ssimCode) => ssimCode switch
    {
        "351" => "A351",
        "789" => "B789",
        "339" => "A339",
        "788" => "B788",
        "77W" => "B77W",
        "744" => "B744",
        "333" => "A333",
        "359" => "A359",
        _     => ssimCode
    };
}

/// <summary>Result of parsing an SSIM file.</summary>
public sealed record SsimParseResult(
    string CarrierCode,
    string SeasonCode,
    string SeasonStart,
    string SeasonEnd,
    IReadOnlyList<SsimFlightRecord> Records);

/// <summary>A single parsed flight leg from a Type 3 SSIM record.</summary>
public sealed record SsimFlightRecord(
    string FlightNumber,
    string Origin,
    string Destination,
    string DepartureTime,
    string ArrivalTime,
    byte ArrivalDayOffset,
    byte DaysOfWeek,
    string AircraftType,
    string ValidFrom,
    string ValidTo,
    string CreatedBy,
    string DepartureTimeUtc,
    string ArrivalTimeUtc,
    byte ArrivalDayOffsetUtc);
