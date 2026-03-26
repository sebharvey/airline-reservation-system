using ReservationSystem.Microservices.Schedule.Application.CreateSchedule;

namespace ReservationSystem.Microservices.Schedule.Application.Ssim;

/// <summary>
/// Parses an IATA SSIM Chapter 7 plain-text file and converts each Type 3
/// flight leg record into a <see cref="CreateScheduleCommand"/>.
///
/// Field positions use the standard SSIM Chapter 7 layout (1-indexed in the
/// spec, 0-indexed in this implementation):
///
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
    public static IReadOnlyList<CreateScheduleCommand> Parse(string ssimText, string createdBy)
    {
        var commands = new List<CreateScheduleCommand>();

        foreach (var rawLine in ssimText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Minimum 68 chars to safely read all required fields; first char must be '3'
            if (line.Length < 68 || line[0] != '3')
                continue;

            // Only import scheduled passenger service
            if (line[10] != 'Y')
                continue;

            var command = TryParseType3(line, createdBy);
            if (command is not null)
                commands.Add(command);
        }

        return commands.AsReadOnly();
    }

    private static CreateScheduleCommand? TryParseType3(string line, string createdBy)
    {
        try
        {
            var carrier    = line.Substring(2, 2).Trim();
            var flightNum  = BuildFlightNumber(carrier, line.Substring(5, 4));
            var periodStart = line.Substring(12, 8);
            var periodEnd   = line.Substring(21, 8);

            if (!DateTime.TryParseExact(periodStart, "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var validFrom))
                return null;

            if (!DateTime.TryParseExact(periodEnd, "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var validTo))
                return null;

            var daysOfWeek = ParseDaysOfWeek(line.Substring(31, 7));
            if (daysOfWeek == 0)
                return null;

            var origin      = line.Substring(39, 3).Trim();
            var destination = line.Substring(55, 3).Trim();

            var departureTime = ParseHhmm(line.Substring(42, 4));
            var arrivalTime   = ParseHhmm(line.Substring(49, 4));
            var arrivalDayOffset = line[53] == '1' ? (byte)1 : (byte)0;

            var aircraftType = ResolveAircraftType(line.Substring(61, 3).Trim());

            return new CreateScheduleCommand(
                flightNum,
                origin,
                destination,
                departureTime,
                arrivalTime,
                arrivalDayOffset,
                daysOfWeek,
                aircraftType,
                validFrom,
                validTo,
                createdBy);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reconstructs the internal flight number from carrier code and the
    /// 4-digit SSIM numeric field.  Leading zeros are stripped from the
    /// numeric part; the result is left-padded to at least 3 digits to
    /// match the convention used throughout the system (e.g. AX001).
    /// Four-or-more significant digit numbers are preserved as-is (AX1001).
    /// </summary>
    private static string BuildFlightNumber(string carrier, string ssimNumeric)
    {
        if (!int.TryParse(ssimNumeric, out var num))
            return carrier + ssimNumeric.TrimStart('0').PadLeft(3, '0');

        var digits = num.ToString();
        if (digits.Length < 3) digits = digits.PadLeft(3, '0');
        return carrier + digits;
    }

    /// <summary>
    /// Converts the 7-character SSIM positional days-of-week string to the
    /// internal bitmask (Mon=bit0 … Sun=bit6).  A non-space character in
    /// position i means the flight operates on day i+1 (Monday=1).
    /// </summary>
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

    /// <summary>Parses a 4-character HHMM string into a <see cref="TimeSpan"/>.</summary>
    private static TimeSpan ParseHhmm(string hhmm)
    {
        var hours   = int.Parse(hhmm.Substring(0, 2));
        var minutes = int.Parse(hhmm.Substring(2, 2));
        return new TimeSpan(hours, minutes, 0);
    }

    /// <summary>
    /// Maps SSIM 3-character equipment codes to the 4-character IATA aircraft
    /// type codes used internally.  Unknown codes are returned unchanged.
    /// </summary>
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
